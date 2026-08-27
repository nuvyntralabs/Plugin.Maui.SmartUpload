namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Lifecycle state of an upload session.
/// </summary>
public enum UploadState
{
	/// <summary>Queued locally and waiting for a concurrency slot or an explicit start.</summary>
	Queued,

	/// <summary>Chunks are being transferred.</summary>
	Uploading,

	/// <summary>Stopped by the caller. Progress is persisted and can be resumed.</summary>
	Paused,

	/// <summary>Every byte was accepted by the remote endpoint.</summary>
	Completed,

	/// <summary>A non-retryable error occurred, or retries were exhausted.</summary>
	Failed,

	/// <summary>Cancelled by the caller. The session remains until <c>RemoveAsync</c>.</summary>
	Cancelled
}
