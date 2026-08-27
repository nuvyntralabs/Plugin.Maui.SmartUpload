using System.Collections.Concurrent;

namespace Plugin.Maui.SmartUpload;

sealed class MemoryUploadStore : IUploadStore
{
	readonly ConcurrentDictionary<string, UploadSessionRecord> _records = new(StringComparer.Ordinal);

	public Task SaveAsync(UploadSessionRecord record, CancellationToken cancellationToken = default)
	{
		_records[record.SessionId] = Clone(record);
		return Task.CompletedTask;
	}

	public Task<UploadSessionRecord?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		_records.TryGetValue(sessionId, out var record);
		return Task.FromResult(record is null ? null : Clone(record));
	}

	public Task<IReadOnlyList<UploadSessionRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<UploadSessionRecord>>(_records.Values.Select(Clone).ToList());

	public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		_records.TryRemove(sessionId, out _);
		return Task.CompletedTask;
	}

	static UploadSessionRecord Clone(UploadSessionRecord record) =>
		SessionSerializer.Deserialize(SessionSerializer.Serialize(record))
		?? throw new InvalidOperationException("Failed to clone upload session.");
}
