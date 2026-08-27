namespace Plugin.Maui.SmartUpload;

/// <summary>
/// A single file slice that will be sent as one HTTP request.
/// </summary>
/// <param name="Index">Zero-based index of this chunk in the remaining plan.</param>
/// <param name="Offset">Absolute byte offset in the source file.</param>
/// <param name="Length">Number of bytes in this chunk.</param>
/// <param name="TotalChunks">Total chunks for the full file using the session chunk size.</param>
public readonly record struct ChunkInfo(int Index, long Offset, int Length, int TotalChunks)
{
	/// <summary>
	/// Inclusive end byte (the last byte of this chunk).
	/// </summary>
	public long EndInclusive => Offset + Length - 1;
}
