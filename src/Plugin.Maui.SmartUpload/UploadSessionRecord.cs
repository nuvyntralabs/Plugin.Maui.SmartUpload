namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Persistence document for one upload session. Custom <see cref="IUploadStore"/> implementations
/// should treat this type as the on-disk contract.
/// </summary>
public sealed class UploadSessionRecord
{
	public string SessionId { get; set; } = "";

	public string FilePath { get; set; } = "";

	public string FileName { get; set; } = "";

	public long FileSize { get; set; }

	public long FileLastWriteTimeUtcTicks { get; set; }

	public string Endpoint { get; set; } = "";

	public string Method { get; set; } = "PUT";

	public string? ContentType { get; set; }

	public Dictionary<string, string> Headers { get; set; } = [];

	public Dictionary<string, string> Metadata { get; set; } = [];

	public int ChunkSize { get; set; } = SmartUploadOptions.DefaultChunkSizeBytes;

	public UploadProtocolKind Protocol { get; set; } = UploadProtocolKind.ContentRange;

	public RetryPolicyData Retry { get; set; } = new();

	public UploadState State { get; set; } = UploadState.Queued;

	public UploadError Error { get; set; }

	public string? LastError { get; set; }

	public long BytesUploaded { get; set; }

	public string? RemoteUploadId { get; set; }

	public long RemoteOffset { get; set; }

	public Dictionary<string, string> ProtocolState { get; set; } = [];

	public DateTimeOffset CreatedAt { get; set; }

	public DateTimeOffset UpdatedAt { get; set; }

	public int AttemptCount { get; set; }
}

/// <summary>
/// Serializable retry settings stored with each session.
/// </summary>
public sealed class RetryPolicyData
{
	public int MaxRetries { get; set; } = 5;

	public double InitialDelayMs { get; set; } = 1000;

	public double MaxDelayMs { get; set; } = 30_000;

	public double BackoffMultiplier { get; set; } = 2;

	public bool UseJitter { get; set; } = true;

	public int[] RetryableStatusCodes { get; set; } = [408, 429, 500, 502, 503, 504];
}
