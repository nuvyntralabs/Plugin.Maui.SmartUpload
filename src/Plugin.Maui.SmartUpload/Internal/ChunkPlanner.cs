namespace Plugin.Maui.SmartUpload;

static class ChunkPlanner
{
	public static int GetCount(long fileSize, int chunkSize)
	{
		if (fileSize <= 0 || chunkSize <= 0)
			return 0;

		return (int)Math.Ceiling(fileSize / (double)chunkSize);
	}

	public static int GetCompletedCount(long bytesUploaded, int chunkSize)
	{
		if (bytesUploaded <= 0 || chunkSize <= 0)
			return 0;

		return (int)(bytesUploaded / chunkSize);
	}

	public static IEnumerable<ChunkInfo> Plan(long fileSize, int chunkSize, long startOffset)
	{
		if (fileSize <= 0 || chunkSize <= 0 || startOffset >= fileSize)
			yield break;

		var total = GetCount(fileSize, chunkSize);
		var offset = startOffset;
		var index = GetCompletedCount(startOffset, chunkSize);

		while (offset < fileSize)
		{
			var length = (int)Math.Min(chunkSize, fileSize - offset);
			yield return new ChunkInfo(index, offset, length, total);
			offset += length;
			index++;
		}
	}
}
