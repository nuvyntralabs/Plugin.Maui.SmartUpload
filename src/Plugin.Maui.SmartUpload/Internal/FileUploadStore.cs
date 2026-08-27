namespace Plugin.Maui.SmartUpload;

sealed class FileUploadStore : IUploadStore
{
	readonly string _directory;
	readonly SemaphoreSlim _gate = new(1, 1);

	public FileUploadStore(string directory)
	{
		_directory = directory;
		Directory.CreateDirectory(_directory);
	}

	public async Task SaveAsync(UploadSessionRecord record, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(record);
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			Directory.CreateDirectory(_directory);
			var path = PathFor(record.SessionId);
			var temp = path + ".tmp";
			var json = SessionSerializer.Serialize(record);
			await File.WriteAllTextAsync(temp, json, cancellationToken).ConfigureAwait(false);
			if (File.Exists(path))
				File.Delete(path);
			File.Move(temp, path);
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task<UploadSessionRecord?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var path = PathFor(sessionId);
			if (!File.Exists(path))
				return null;

			var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
			return SessionSerializer.Deserialize(json);
		}
		catch (IOException)
		{
			return null;
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task<IReadOnlyList<UploadSessionRecord>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (!Directory.Exists(_directory))
				return [];

			var records = new List<UploadSessionRecord>();
			foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
					var record = SessionSerializer.Deserialize(json);
					if (record is not null)
						records.Add(record);
				}
				catch (IOException)
				{
					// Skip a corrupt or locked file.
				}
			}

			return records;
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var path = PathFor(sessionId);
			if (File.Exists(path))
				File.Delete(path);
		}
		finally
		{
			_gate.Release();
		}
	}

	string PathFor(string sessionId)
	{
		foreach (var c in sessionId)
		{
			if (!char.IsLetterOrDigit(c) && c is not '-' and not '_')
				throw new SmartUploadException(UploadError.InvalidRequest, $"Session id '{sessionId}' is not a valid file name.");
		}

		return Path.Combine(_directory, sessionId + ".json");
	}
}
