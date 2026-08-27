namespace Plugin.Maui.SmartUpload;

static class RequestValidator
{
	public static void Validate(UploadRequest request, SmartUploadOptions options)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(request.FilePath))
			throw new SmartUploadException(UploadError.InvalidRequest, "FilePath is required.");

		if (!File.Exists(request.FilePath))
			throw new SmartUploadException(UploadError.FileNotFound, $"File not found: {request.FilePath}");

		var info = new FileInfo(request.FilePath);
		if (info.Length <= 0)
			throw new SmartUploadException(UploadError.InvalidRequest, "The file is empty.");

		if (request.Endpoint is null)
			throw new SmartUploadException(UploadError.InvalidRequest, "Endpoint is required.");

		if (request.Endpoint.Scheme is not "http" and not "https")
			throw new SmartUploadException(UploadError.InvalidRequest, "Endpoint must be an http or https URL.");

		if (request.SessionId is { Length: > 0 })
			ValidateSessionId(request.SessionId);

		if (request.ChunkSize is int chunkSize && chunkSize < 1)
			throw new SmartUploadException(UploadError.InvalidRequest, "ChunkSize must be positive.");

		if (request.Protocol == UploadProtocolKind.Custom && request.CustomProtocol is null && options.CustomProtocol is null)
			throw new SmartUploadException(UploadError.InvalidRequest, "A custom protocol instance is required when Protocol is Custom.");
	}

	public static void ValidateSessionId(string sessionId)
	{
		if (string.IsNullOrWhiteSpace(sessionId))
			throw new SmartUploadException(UploadError.InvalidRequest, "Session id is required.");

		if (sessionId.Length > 128)
			throw new SmartUploadException(UploadError.InvalidRequest, "Session id cannot exceed 128 characters.");

		foreach (var c in sessionId)
		{
			if (!char.IsLetterOrDigit(c) && c is not '-' and not '_')
				throw new SmartUploadException(UploadError.InvalidRequest, "Session id may contain only letters, digits, '-' and '_'.");
		}
	}

	public static void EnsureFileUnchanged(UploadSessionRecord record)
	{
		if (!File.Exists(record.FilePath))
			throw new SmartUploadException(UploadError.FileNotFound, $"File no longer exists: {record.FilePath}");

		var info = new FileInfo(record.FilePath);
		if (info.Length != record.FileSize || info.LastWriteTimeUtc.Ticks != record.FileLastWriteTimeUtcTicks)
			throw new SmartUploadException(UploadError.FileChanged, "The source file changed after the session was created.");
	}
}
