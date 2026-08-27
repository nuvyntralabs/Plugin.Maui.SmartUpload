namespace Plugin.Maui.SmartUpload;

sealed class FileSliceStream : Stream
{
	readonly FileStream _file;
	readonly long _start;
	readonly long _length;
	readonly Action<int>? _onRead;
	long _position;
	bool _disposed;

	public FileSliceStream(string path, long start, long length, Action<int>? onRead = null)
	{
		if (start < 0)
			throw new ArgumentOutOfRangeException(nameof(start));
		if (length < 0)
			throw new ArgumentOutOfRangeException(nameof(length));

		_file = new FileStream(
			path,
			FileMode.Open,
			FileAccess.Read,
			FileShare.Read,
			bufferSize: 64 * 1024,
			FileOptions.Asynchronous | FileOptions.SequentialScan);

		_start = start;
		_length = length;
		_onRead = onRead;
		_file.Seek(start, SeekOrigin.Begin);
	}

	public override bool CanRead => true;
	public override bool CanSeek => true;
	public override bool CanWrite => false;
	public override long Length => _length;

	public override long Position
	{
		get => _position;
		set => Seek(value, SeekOrigin.Begin);
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		var toRead = ClampCount(count);
		if (toRead == 0)
			return 0;

		var read = _file.Read(buffer, offset, toRead);
		Advance(read);
		return read;
	}

	public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		var toRead = ClampCount(count);
		if (toRead == 0)
			return 0;

		var read = await _file.ReadAsync(buffer.AsMemory(offset, toRead), cancellationToken).ConfigureAwait(false);
		Advance(read);
		return read;
	}

	public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		var toRead = ClampCount(buffer.Length);
		if (toRead == 0)
			return 0;

		var read = await _file.ReadAsync(buffer[..toRead], cancellationToken).ConfigureAwait(false);
		Advance(read);
		return read;
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		var newPosition = origin switch
		{
			SeekOrigin.Begin => offset,
			SeekOrigin.Current => _position + offset,
			SeekOrigin.End => _length + offset,
			_ => throw new ArgumentOutOfRangeException(nameof(origin))
		};

		if (newPosition < 0 || newPosition > _length)
			throw new IOException("Attempted to seek outside the file slice.");

		_position = newPosition;
		_file.Seek(_start + _position, SeekOrigin.Begin);
		return _position;
	}

	public override void Flush()
	{
	}

	public override void SetLength(long value) => throw new NotSupportedException();

	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	protected override void Dispose(bool disposing)
	{
		if (!_disposed && disposing)
		{
			_file.Dispose();
			_disposed = true;
		}

		base.Dispose(disposing);
	}

	int ClampCount(int count)
	{
		var remaining = _length - _position;
		if (remaining <= 0)
			return 0;

		return (int)Math.Min(count, remaining);
	}

	void Advance(int read)
	{
		if (read <= 0)
			return;

		_position += read;
		_onRead?.Invoke(read);
	}
}
