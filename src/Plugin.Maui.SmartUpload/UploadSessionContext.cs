namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Mutable view of a session given to <see cref="IUploadProtocol"/> implementations.
/// Changes to remote identifiers are persisted after initialize, query, and each chunk.
/// </summary>
public sealed class UploadSessionContext
{
	public required string SessionId { get; init; }

	public required Uri Endpoint { get; set; }

	public required string FilePath { get; init; }

	public required string FileName { get; init; }

	public required long FileSize { get; init; }

	public string? ContentType { get; init; }

	public required HttpMethod Method { get; init; }

	public required int ChunkSize { get; init; }

	public IDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

	/// <summary>
	/// Server-assigned upload id (tus <c>Location</c> filename, or a custom id).
	/// </summary>
	public string? RemoteUploadId { get; set; }

	/// <summary>
	/// Highest byte offset acknowledged by the server.
	/// </summary>
	public long RemoteOffset { get; set; }

	/// <summary>
	/// Extra key/value pairs persisted with the session (for example a tus <c>Location</c> URL).
	/// </summary>
	public IDictionary<string, string> ProtocolState { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
