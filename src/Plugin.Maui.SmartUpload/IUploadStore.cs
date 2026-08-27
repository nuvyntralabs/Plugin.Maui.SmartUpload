namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Persists upload sessions so they can be resumed after process death.
/// </summary>
public interface IUploadStore
{
	Task SaveAsync(UploadSessionRecord record, CancellationToken cancellationToken = default);

	Task<UploadSessionRecord?> GetAsync(string sessionId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<UploadSessionRecord>> GetAllAsync(CancellationToken cancellationToken = default);

	Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default);
}
