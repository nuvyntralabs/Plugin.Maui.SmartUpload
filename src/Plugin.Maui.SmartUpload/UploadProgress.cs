namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Byte-level progress for an in-flight or persisted session.
/// </summary>
public sealed class UploadProgress
{
	/// <summary>
	/// Bytes known to be accepted (completed chunks plus in-flight bytes of the current chunk).
	/// </summary>
	public required long BytesUploaded { get; init; }

	public required long TotalBytes { get; init; }

	public required int CompletedChunks { get; init; }

	public required int TotalChunks { get; init; }

	/// <summary>
	/// <c>BytesUploaded / TotalBytes</c>, clamped to <c>0..1</c>.
	/// </summary>
	public double Fraction => TotalBytes <= 0
		? 0
		: Math.Clamp(BytesUploaded / (double)TotalBytes, 0d, 1d);

	/// <summary>
	/// Index of the chunk currently being sent, when known.
	/// </summary>
	public int? CurrentChunkIndex { get; init; }
}
