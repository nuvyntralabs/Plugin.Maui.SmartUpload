namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Describes a file that should be uploaded in resumable chunks.
/// </summary>
public sealed class UploadRequest
{
	/// <summary>
	/// Optional caller-supplied id. When omitted, a GUID is generated.
	/// </summary>
	public string? SessionId { get; set; }

	/// <summary>
	/// Absolute path of the file to upload. The file must remain available until the session completes.
	/// </summary>
	public required string FilePath { get; set; }

	/// <summary>
	/// Destination URL. For tus this is the creation endpoint; the plugin follows <c>Location</c>.
	/// </summary>
	public required Uri Endpoint { get; set; }

	/// <summary>
	/// HTTP method used by the Content-Range protocol. Ignored by tus (which uses POST/HEAD/PATCH).
	/// </summary>
	public HttpMethod Method { get; set; } = HttpMethod.Put;

	/// <summary>
	/// Display name sent to the server. Defaults to the file name.
	/// </summary>
	public string? FileName { get; set; }

	/// <summary>
	/// MIME type. Inferred from the extension when omitted.
	/// </summary>
	public string? ContentType { get; set; }

	/// <summary>
	/// Extra headers copied onto every request (authorization, API keys, etc.).
	/// </summary>
	public Dictionary<string, string> Headers { get; set; } = [];

	/// <summary>
	/// Opaque application metadata persisted with the session and, for tus, encoded as <c>Upload-Metadata</c>.
	/// </summary>
	public Dictionary<string, string> Metadata { get; set; } = [];

	/// <summary>
	/// Chunk size in bytes. Falls back to <see cref="SmartUploadOptions.DefaultChunkSize"/>.
	/// </summary>
	public int? ChunkSize { get; set; }

	/// <summary>
	/// Per-session retry policy. Falls back to <see cref="SmartUploadOptions.DefaultRetry"/>.
	/// </summary>
	public RetryPolicy? Retry { get; set; }

	/// <summary>
	/// Wire protocol. Defaults to <see cref="UploadProtocolKind.ContentRange"/>.
	/// </summary>
	public UploadProtocolKind Protocol { get; set; } = UploadProtocolKind.ContentRange;

	/// <summary>
	/// Optional protocol instance used when <see cref="Protocol"/> is <see cref="UploadProtocolKind.Custom"/>.
	/// </summary>
	public IUploadProtocol? CustomProtocol { get; set; }

	/// <summary>
	/// When <see langword="true"/>, the session starts as soon as a concurrency slot is available.
	/// </summary>
	public bool AutoStart { get; set; } = true;
}
