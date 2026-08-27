namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Cross-platform client for chunked, resumable file uploads.
/// </summary>
public interface ISmartUploadClient
{
	/// <summary>
	/// Gets a value indicating whether plugin logging is currently enabled.
	/// </summary>
	bool IsLoggingEnabled { get; }

	event EventHandler<UploadProgressEventArgs>? ProgressChanged;

	event EventHandler<UploadSessionEventArgs>? SessionStateChanged;

	event EventHandler<UploadCompletedEventArgs>? SessionCompleted;

	event EventHandler<UploadFailedEventArgs>? SessionFailed;

	/// <summary>
	/// Enables or disables plugin logging.
	/// </summary>
	void EnableLogging(bool enabled, ISmartUploadLogger? logger = null);

	/// <summary>
	/// Creates a persisted session. Starts automatically when <see cref="UploadRequest.AutoStart"/> is true.
	/// </summary>
	Task<UploadSession> EnqueueAsync(UploadRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Starts or continues a queued, paused, or failed session.
	/// </summary>
	Task StartAsync(string sessionId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Stops the current chunk after it is cancelled and keeps progress on disk.
	/// </summary>
	Task PauseAsync(string sessionId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Continues a paused or interrupted session from the last acknowledged byte.
	/// </summary>
	Task ResumeAsync(string sessionId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Cancels an in-flight or paused session. The record remains until <see cref="RemoveAsync"/>.
	/// </summary>
	Task CancelAsync(string sessionId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Starts a failed or cancelled session again from the persisted offset.
	/// </summary>
	Task RetryAsync(string sessionId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Cancels if needed and deletes the persisted session.
	/// </summary>
	Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the latest snapshot, or <see langword="null"/> when the id is unknown.
	/// </summary>
	Task<UploadSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns every persisted session, newest first.
	/// </summary>
	Task<IReadOnlyList<UploadSession>> GetSessionsAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Resumes sessions that were uploading when the process died, plus any queued auto-start work.
	/// </summary>
	Task ResumeInterruptedAsync(CancellationToken cancellationToken = default);
}
