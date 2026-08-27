namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Immutable snapshot of a persisted upload session.
/// </summary>
public sealed class UploadSession
{
	public required string SessionId { get; init; }

	public required string FilePath { get; init; }

	public required string FileName { get; init; }

	public required long FileSize { get; init; }

	public required Uri Endpoint { get; init; }

	public required UploadState State { get; init; }

	public required UploadProtocolKind Protocol { get; init; }

	public required int ChunkSize { get; init; }

	public required long BytesUploaded { get; init; }

	public required int CompletedChunks { get; init; }

	public required int TotalChunks { get; init; }

	public required DateTimeOffset CreatedAt { get; init; }

	public required DateTimeOffset UpdatedAt { get; init; }

	public UploadError Error { get; init; }

	public string? LastError { get; init; }

	public string? RemoteUploadId { get; init; }

	public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

	public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

	public UploadProgress Progress => new()
	{
		BytesUploaded = BytesUploaded,
		TotalBytes = FileSize,
		CompletedChunks = CompletedChunks,
		TotalChunks = TotalChunks
	};
}
