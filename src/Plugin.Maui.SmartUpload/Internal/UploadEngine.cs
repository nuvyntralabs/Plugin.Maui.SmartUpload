namespace Plugin.Maui.SmartUpload;

sealed class UploadEngine(
	IUploadStore store,
	Func<UploadSessionRecord, IUploadProtocol> protocolFactory,
	ISmartUploadLogger? logger,
	Action<UploadSession, UploadProgress> onProgress,
	Action<UploadSession> onStateChanged)
{
	public async Task RunAsync(string sessionId, CancellationToken cancellationToken)
	{
		var record = await store.GetAsync(sessionId, cancellationToken).ConfigureAwait(false)
			?? throw new SmartUploadException(UploadError.InvalidRequest, $"Unknown session '{sessionId}'.");

		RequestValidator.EnsureFileUnchanged(record);

		var protocol = protocolFactory(record);
		var context = SessionMapper.ToContext(record);
		var retry = RetryPolicy.FromData(record.Retry);

		record.State = UploadState.Uploading;
		record.Error = UploadError.None;
		record.LastError = null;
		record.UpdatedAt = DateTimeOffset.UtcNow;
		await PersistAsync(record, cancellationToken).ConfigureAwait(false);
		RaiseState(record);

		try
		{
			await protocol.InitializeAsync(context, cancellationToken).ConfigureAwait(false);
			SessionMapper.ApplyContext(record, context);
			await PersistAsync(record, cancellationToken).ConfigureAwait(false);

			await protocol.QueryRemoteProgressAsync(context, cancellationToken).ConfigureAwait(false);
			SessionMapper.ApplyContext(record, context);
			record.BytesUploaded = Math.Max(record.BytesUploaded, context.RemoteOffset);
			await PersistAsync(record, cancellationToken).ConfigureAwait(false);
			RaiseProgress(record, inFlight: 0, currentChunk: null);

			foreach (var chunk in ChunkPlanner.Plan(record.FileSize, record.ChunkSize, record.BytesUploaded))
			{
				cancellationToken.ThrowIfCancellationRequested();
				RequestValidator.EnsureFileUnchanged(record);

				var inFlight = 0;
				await RetryExecutor.ExecuteAsync(
					async token =>
					{
						inFlight = 0;
						using var slice = new FileSliceStream(record.FilePath, chunk.Offset, chunk.Length, read =>
						{
							inFlight += read;
							RaiseProgress(record, inFlight, chunk.Index);
						});

						await protocol.UploadChunkAsync(context, chunk, slice, token).ConfigureAwait(false);
					},
					retry,
					(attempt, exception, delay) =>
					{
						record.AttemptCount++;
						Log(SmartUploadLogLevel.Warning, $"Session {sessionId} chunk {chunk.Index} failed (attempt {attempt}). Retrying in {delay.TotalMilliseconds:0} ms.", exception);
					},
					cancellationToken).ConfigureAwait(false);

				SessionMapper.ApplyContext(record, context);
				record.BytesUploaded = Math.Max(context.RemoteOffset, chunk.Offset + chunk.Length);
				record.UpdatedAt = DateTimeOffset.UtcNow;
				await PersistAsync(record, cancellationToken).ConfigureAwait(false);
				RaiseProgress(record, inFlight: 0, chunk.Index);
			}

			await protocol.CompleteAsync(context, cancellationToken).ConfigureAwait(false);
			SessionMapper.ApplyContext(record, context);
			record.BytesUploaded = record.FileSize;
			record.State = UploadState.Completed;
			record.Error = UploadError.None;
			record.LastError = null;
			record.UpdatedAt = DateTimeOffset.UtcNow;
			await PersistAsync(record, CancellationToken.None).ConfigureAwait(false);
			RaiseProgress(record, 0, null);
			RaiseState(record);
			Log(SmartUploadLogLevel.Information, $"Session {sessionId} completed ({record.FileSize} bytes).");
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (SmartUploadException ex)
		{
			await FailAsync(record, ex.Error, ex.Message, ex).ConfigureAwait(false);
			throw;
		}
		catch (Exception ex)
		{
			await FailAsync(record, UploadError.Unknown, ex.Message, ex).ConfigureAwait(false);
			throw new SmartUploadException(UploadError.Unknown, ex.Message, ex);
		}
	}

	public async Task AbortRemoteAsync(UploadSessionRecord record, CancellationToken cancellationToken)
	{
		try
		{
			var protocol = protocolFactory(record);
			var context = SessionMapper.ToContext(record);
			await protocol.AbortAsync(context, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Log(SmartUploadLogLevel.Warning, $"Failed to abort remote upload for {record.SessionId}.", ex);
		}
	}

	async Task FailAsync(UploadSessionRecord record, UploadError error, string message, Exception exception)
	{
		record.State = UploadState.Failed;
		record.Error = error;
		record.LastError = message;
		record.UpdatedAt = DateTimeOffset.UtcNow;
		await PersistAsync(record, CancellationToken.None).ConfigureAwait(false);
		RaiseState(record);
		Log(SmartUploadLogLevel.Error, $"Session {record.SessionId} failed: {message}", exception);
	}

	async Task PersistAsync(UploadSessionRecord record, CancellationToken cancellationToken)
	{
		try
		{
			await store.SaveAsync(record, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new SmartUploadException(UploadError.Persistence, "Failed to persist upload session.", ex);
		}
	}

	void RaiseProgress(UploadSessionRecord record, long inFlight, int? currentChunk)
	{
		var snapshot = SessionMapper.ToSnapshot(record);
		var progress = new UploadProgress
		{
			BytesUploaded = Math.Min(record.FileSize, record.BytesUploaded + inFlight),
			TotalBytes = record.FileSize,
			CompletedChunks = ChunkPlanner.GetCompletedCount(record.BytesUploaded, record.ChunkSize),
			TotalChunks = ChunkPlanner.GetCount(record.FileSize, record.ChunkSize),
			CurrentChunkIndex = currentChunk
		};
		onProgress(snapshot, progress);
	}

	void RaiseState(UploadSessionRecord record) => onStateChanged(SessionMapper.ToSnapshot(record));

	void Log(SmartUploadLogLevel level, string message, Exception? exception = null) =>
		logger?.Log(level, message, exception);
}
