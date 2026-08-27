using System.Collections.Concurrent;
using Microsoft.Maui.Storage;

namespace Plugin.Maui.SmartUpload;

sealed class SmartUploadClient : ISmartUploadClient, IDisposable
{
	readonly SmartUploadOptions _options;
	readonly IUploadStore _store;
	readonly HttpClient _http;
	readonly bool _ownsHttp;
	readonly SemaphoreSlim _concurrency;
	readonly ConcurrentDictionary<string, SessionRuntime> _runtimes = new(StringComparer.Ordinal);
	readonly Dictionary<UploadProtocolKind, IUploadProtocol> _protocols;
	readonly IUploadProtocol? _customProtocol;
	ISmartUploadLogger? _logger;
	bool _loggingEnabled;
	bool _disposed;

	SmartUploadClient(SmartUploadOptions options, IUploadStore store, HttpClient http, bool ownsHttp)
	{
		_options = options;
		_store = store;
		_http = http;
		_ownsHttp = ownsHttp;
		_concurrency = new SemaphoreSlim(options.ResolveMaxConcurrency(), options.ResolveMaxConcurrency());
		_customProtocol = options.CustomProtocol;
		_protocols = new Dictionary<UploadProtocolKind, IUploadProtocol>
		{
			[UploadProtocolKind.ContentRange] = new ContentRangeProtocol(http),
			[UploadProtocolKind.Tus] = new TusProtocol(http, options)
		};
		_loggingEnabled = options.EnableLogging;
		_logger = options.Logger ?? (_loggingEnabled ? new DebugSmartUploadLogger() : null);
	}

	public static SmartUploadClient Create(SmartUploadOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var store = options.Store ?? new FileUploadStore(ResolveStorageDirectory(options.StorageDirectory));
		HttpClient http;
		var ownsHttp = false;
		if (options.HttpClient is not null)
		{
			http = options.HttpClient;
			if (options.HttpTimeout > TimeSpan.Zero)
				http.Timeout = options.HttpTimeout;
		}
		else
		{
			http = new HttpClient { Timeout = options.HttpTimeout };
			ownsHttp = true;
		}

		return new SmartUploadClient(options, store, http, ownsHttp);
	}

	public bool IsLoggingEnabled => _loggingEnabled;

	public event EventHandler<UploadProgressEventArgs>? ProgressChanged;
	public event EventHandler<UploadSessionEventArgs>? SessionStateChanged;
	public event EventHandler<UploadCompletedEventArgs>? SessionCompleted;
	public event EventHandler<UploadFailedEventArgs>? SessionFailed;

	public void EnableLogging(bool enabled, ISmartUploadLogger? logger = null)
	{
		_loggingEnabled = enabled;
		_logger = enabled ? logger ?? _logger ?? new DebugSmartUploadLogger() : logger;
	}

	public async Task<UploadSession> EnqueueAsync(UploadRequest request, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		RequestValidator.Validate(request, _options);

		var record = SessionMapper.FromRequest(request, _options);
		if (await _store.GetAsync(record.SessionId, cancellationToken).ConfigureAwait(false) is not null)
			throw new SmartUploadException(UploadError.Conflict, $"Session '{record.SessionId}' already exists.");

		await _store.SaveAsync(record, cancellationToken).ConfigureAwait(false);
		var snapshot = SessionMapper.ToSnapshot(record);
		RaiseState(snapshot);
		Log(SmartUploadLogLevel.Information, $"Enqueued {record.SessionId} ({record.FileName}, {record.FileSize} bytes).");

		if (request.AutoStart)
			StartCore(record.SessionId, request.CustomProtocol);

		return await GetRequiredSessionAsync(record.SessionId, cancellationToken).ConfigureAwait(false);
	}

	public async Task StartAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		RequestValidator.ValidateSessionId(sessionId);

		var record = await _store.GetAsync(sessionId, cancellationToken).ConfigureAwait(false)
			?? throw new SmartUploadException(UploadError.InvalidRequest, $"Unknown session '{sessionId}'.");

		if (record.State == UploadState.Completed)
			return;

		StartCore(sessionId, customProtocol: null);
	}

	public Task ResumeAsync(string sessionId, CancellationToken cancellationToken = default) =>
		StartAsync(sessionId, cancellationToken);

	public Task RetryAsync(string sessionId, CancellationToken cancellationToken = default) =>
		StartAsync(sessionId, cancellationToken);

	public async Task PauseAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		RequestValidator.ValidateSessionId(sessionId);

		var runtime = _runtimes.GetOrAdd(sessionId, _ => new SessionRuntime());
		runtime.RequestedState = UploadState.Paused;
		runtime.WorkCts?.Cancel();

		var running = runtime.RunningTask;
		if (running is not null)
		{
			try
			{
				await running.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
			}
		}

		var record = await _store.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
		if (record is null)
			return;

		if (record.State is UploadState.Completed or UploadState.Cancelled)
			return;

		record.State = UploadState.Paused;
		record.UpdatedAt = DateTimeOffset.UtcNow;
		await _store.SaveAsync(record, cancellationToken).ConfigureAwait(false);
		RaiseState(SessionMapper.ToSnapshot(record));
		Log(SmartUploadLogLevel.Information, $"Paused {sessionId} at {record.BytesUploaded} bytes.");
	}

	public async Task CancelAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		RequestValidator.ValidateSessionId(sessionId);

		var runtime = _runtimes.GetOrAdd(sessionId, _ => new SessionRuntime());
		runtime.RequestedState = UploadState.Cancelled;
		runtime.WorkCts?.Cancel();

		var running = runtime.RunningTask;
		if (running is not null)
		{
			try
			{
				await running.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
			}
		}

		var record = await _store.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
		if (record is null)
			return;

		if (record.State != UploadState.Completed)
		{
			await CreateEngine().AbortRemoteAsync(record, cancellationToken).ConfigureAwait(false);
			record.State = UploadState.Cancelled;
			record.Error = UploadError.Cancelled;
			record.LastError = "Cancelled by the user.";
			record.UpdatedAt = DateTimeOffset.UtcNow;
			await _store.SaveAsync(record, cancellationToken).ConfigureAwait(false);
			RaiseState(SessionMapper.ToSnapshot(record));
		}

		Log(SmartUploadLogLevel.Information, $"Cancelled {sessionId}.");
	}

	public async Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		await CancelAsync(sessionId, cancellationToken).ConfigureAwait(false);
		await _store.DeleteAsync(sessionId, cancellationToken).ConfigureAwait(false);
		_runtimes.TryRemove(sessionId, out _);
		Log(SmartUploadLogLevel.Information, $"Removed {sessionId}.");
	}

	public async Task<UploadSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		RequestValidator.ValidateSessionId(sessionId);
		var record = await _store.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
		return record is null ? null : SessionMapper.ToSnapshot(record);
	}

	public async Task<IReadOnlyList<UploadSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		var records = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);
		return records
			.Select(SessionMapper.ToSnapshot)
			.OrderByDescending(session => session.UpdatedAt)
			.ToList();
	}

	public async Task ResumeInterruptedAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		var records = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);
		foreach (var record in records)
		{
			if (record.State is UploadState.Uploading or UploadState.Queued)
				StartCore(record.SessionId, customProtocol: null);
		}
	}

	void StartCore(string sessionId, IUploadProtocol? customProtocol)
	{
		var runtime = _runtimes.GetOrAdd(sessionId, _ => new SessionRuntime());
		lock (runtime.Gate)
		{
			if (runtime.RunningTask is { IsCompleted: false })
				return;

			runtime.RequestedState = UploadState.Uploading;
			runtime.WorkCts?.Dispose();
			runtime.WorkCts = new CancellationTokenSource();
			var token = runtime.WorkCts.Token;
			runtime.RunningTask = Task.Run(() => RunSessionAsync(sessionId, customProtocol, runtime, token), CancellationToken.None);
		}
	}

	async Task RunSessionAsync(string sessionId, IUploadProtocol? customProtocol, SessionRuntime runtime, CancellationToken cancellationToken)
	{
		await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (runtime.RequestedState is UploadState.Paused or UploadState.Cancelled)
				return;

			if (customProtocol is not null)
				runtime.CustomProtocol = customProtocol;

			await CreateEngine().RunAsync(sessionId, cancellationToken).ConfigureAwait(false);

			var completed = await _store.GetAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
			if (completed?.State == UploadState.Completed)
				SessionCompleted?.Invoke(this, new UploadCompletedEventArgs { Session = SessionMapper.ToSnapshot(completed) });
		}
		catch (OperationCanceledException) when (runtime.RequestedState == UploadState.Paused)
		{
			var record = await _store.GetAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
			if (record is not null && record.State is UploadState.Uploading or UploadState.Queued)
			{
				record.State = UploadState.Paused;
				record.UpdatedAt = DateTimeOffset.UtcNow;
				await _store.SaveAsync(record, CancellationToken.None).ConfigureAwait(false);
				RaiseState(SessionMapper.ToSnapshot(record));
			}
		}
		catch (OperationCanceledException) when (runtime.RequestedState == UploadState.Cancelled)
		{
			// CancelAsync persists the cancelled state.
		}
		catch (Exception ex)
		{
			var record = await _store.GetAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
			if (record is not null)
			{
				SessionFailed?.Invoke(this, new UploadFailedEventArgs
				{
					Session = SessionMapper.ToSnapshot(record),
					Error = record.Error == UploadError.None ? UploadError.Unknown : record.Error,
					Message = record.LastError ?? ex.Message,
					Exception = ex
				});
			}
		}
		finally
		{
			_concurrency.Release();
		}
	}

	UploadEngine CreateEngine() =>
		new(
			_store,
			ResolveProtocol,
			_loggingEnabled ? _logger : null,
			(session, progress) => ProgressChanged?.Invoke(this, new UploadProgressEventArgs { Session = session, Progress = progress }),
			RaiseState);

	IUploadProtocol ResolveProtocol(UploadSessionRecord record)
	{
		if (record.Protocol == UploadProtocolKind.Custom)
		{
			if (_runtimes.TryGetValue(record.SessionId, out var runtime) && runtime.CustomProtocol is not null)
				return runtime.CustomProtocol;
			if (_customProtocol is not null)
				return _customProtocol;
			throw new SmartUploadException(UploadError.Protocol, "No custom protocol is registered.");
		}

		if (_protocols.TryGetValue(record.Protocol, out var protocol))
			return protocol;

		throw new SmartUploadException(UploadError.Protocol, $"Unsupported protocol '{record.Protocol}'.");
	}

	async Task<UploadSession> GetRequiredSessionAsync(string sessionId, CancellationToken cancellationToken)
	{
		var session = await GetSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
		return session ?? throw new SmartUploadException(UploadError.Persistence, $"Session '{sessionId}' was not saved.");
	}

	void RaiseState(UploadSession session) =>
		SessionStateChanged?.Invoke(this, new UploadSessionEventArgs { Session = session });

	void Log(SmartUploadLogLevel level, string message, Exception? exception = null)
	{
		if (_loggingEnabled)
			_logger?.Log(level, message, exception);
	}

	void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

	static string ResolveStorageDirectory(string? configured)
	{
		if (!string.IsNullOrWhiteSpace(configured))
			return configured;

		try
		{
			return Path.Combine(FileSystem.AppDataDirectory, "Plugin.Maui.SmartUpload");
		}
		catch
		{
			return Path.Combine(Path.GetTempPath(), "Plugin.Maui.SmartUpload");
		}
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		foreach (var runtime in _runtimes.Values)
		{
			runtime.WorkCts?.Cancel();
			runtime.WorkCts?.Dispose();
		}

		_concurrency.Dispose();
		if (_ownsHttp)
			_http.Dispose();
	}

	sealed class SessionRuntime
	{
		public object Gate { get; } = new();
		public CancellationTokenSource? WorkCts { get; set; }
		public Task? RunningTask { get; set; }
		public UploadState RequestedState { get; set; } = UploadState.Queued;
		public IUploadProtocol? CustomProtocol { get; set; }
	}
}
