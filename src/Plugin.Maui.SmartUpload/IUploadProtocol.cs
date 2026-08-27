namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Pluggable HTTP contract for creating, querying, and sending chunks.
/// </summary>
public interface IUploadProtocol
{
	UploadProtocolKind Kind { get; }

	/// <summary>
	/// Creates a remote upload when needed (tus POST, custom handshake). Must be idempotent.
	/// </summary>
	Task InitializeAsync(UploadSessionContext context, CancellationToken cancellationToken);

	/// <summary>
	/// Asks the server how far the upload has progressed and writes <see cref="UploadSessionContext.RemoteOffset"/>.
	/// </summary>
	Task QueryRemoteProgressAsync(UploadSessionContext context, CancellationToken cancellationToken);

	/// <summary>
	/// Sends one file slice. The stream length equals <see cref="ChunkInfo.Length"/>.
	/// </summary>
	Task UploadChunkAsync(UploadSessionContext context, ChunkInfo chunk, Stream chunkStream, CancellationToken cancellationToken);

	/// <summary>
	/// Optional completion handshake after the last byte has been accepted.
	/// </summary>
	Task CompleteAsync(UploadSessionContext context, CancellationToken cancellationToken);

	/// <summary>
	/// Optional remote cleanup when the user cancels. Default implementations may no-op.
	/// </summary>
	Task AbortAsync(UploadSessionContext context, CancellationToken cancellationToken);
}
