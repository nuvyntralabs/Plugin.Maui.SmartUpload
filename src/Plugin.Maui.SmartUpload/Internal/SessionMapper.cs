namespace Plugin.Maui.SmartUpload;

static class SessionMapper
{
	public static UploadSession ToSnapshot(UploadSessionRecord record)
	{
		var totalChunks = ChunkPlanner.GetCount(record.FileSize, record.ChunkSize);
		return new UploadSession
		{
			SessionId = record.SessionId,
			FilePath = record.FilePath,
			FileName = record.FileName,
			FileSize = record.FileSize,
			Endpoint = new Uri(record.Endpoint, UriKind.Absolute),
			State = record.State,
			Protocol = record.Protocol,
			ChunkSize = record.ChunkSize,
			BytesUploaded = record.BytesUploaded,
			CompletedChunks = ChunkPlanner.GetCompletedCount(record.BytesUploaded, record.ChunkSize),
			TotalChunks = totalChunks,
			CreatedAt = record.CreatedAt,
			UpdatedAt = record.UpdatedAt,
			Error = record.Error,
			LastError = record.LastError,
			RemoteUploadId = record.RemoteUploadId,
			Metadata = new Dictionary<string, string>(record.Metadata, StringComparer.Ordinal),
			Headers = new Dictionary<string, string>(record.Headers, StringComparer.OrdinalIgnoreCase)
		};
	}

	public static UploadSessionContext ToContext(UploadSessionRecord record)
	{
		var endpoint = record.ProtocolState.TryGetValue(TusProtocol.LocationKey, out var location) && !string.IsNullOrWhiteSpace(location)
			? new Uri(location, UriKind.Absolute)
			: new Uri(record.Endpoint, UriKind.Absolute);

		return new UploadSessionContext
		{
			SessionId = record.SessionId,
			Endpoint = endpoint,
			FilePath = record.FilePath,
			FileName = record.FileName,
			FileSize = record.FileSize,
			ContentType = record.ContentType,
			Method = new HttpMethod(record.Method),
			ChunkSize = record.ChunkSize,
			Headers = new Dictionary<string, string>(record.Headers, StringComparer.OrdinalIgnoreCase),
			Metadata = new Dictionary<string, string>(record.Metadata, StringComparer.Ordinal),
			RemoteUploadId = record.RemoteUploadId,
			RemoteOffset = record.RemoteOffset,
			ProtocolState = new Dictionary<string, string>(record.ProtocolState, StringComparer.Ordinal)
		};
	}

	public static void ApplyContext(UploadSessionRecord record, UploadSessionContext context)
	{
		record.RemoteUploadId = context.RemoteUploadId;
		record.RemoteOffset = context.RemoteOffset;
		record.ProtocolState = new Dictionary<string, string>(context.ProtocolState, StringComparer.Ordinal);
		if (context.ProtocolState.TryGetValue(TusProtocol.LocationKey, out var location) && !string.IsNullOrWhiteSpace(location))
			record.Endpoint = location;
	}

	public static UploadSessionRecord FromRequest(UploadRequest request, SmartUploadOptions options)
	{
		var info = new FileInfo(request.FilePath);
		var now = DateTimeOffset.UtcNow;
		var fileName = string.IsNullOrWhiteSpace(request.FileName) ? info.Name : request.FileName;

		return new UploadSessionRecord
		{
			SessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId.Trim(),
			FilePath = info.FullName,
			FileName = fileName!,
			FileSize = info.Length,
			FileLastWriteTimeUtcTicks = info.LastWriteTimeUtc.Ticks,
			Endpoint = request.Endpoint.AbsoluteUri,
			Method = request.Method.Method,
			ContentType = ContentTypeResolver.Resolve(fileName!, request.ContentType),
			Headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase),
			Metadata = new Dictionary<string, string>(request.Metadata, StringComparer.Ordinal),
			ChunkSize = options.ResolveChunkSize(request.ChunkSize),
			Protocol = request.Protocol,
			Retry = (request.Retry ?? options.DefaultRetry).ToData(),
			State = UploadState.Queued,
			CreatedAt = now,
			UpdatedAt = now
		};
	}
}
