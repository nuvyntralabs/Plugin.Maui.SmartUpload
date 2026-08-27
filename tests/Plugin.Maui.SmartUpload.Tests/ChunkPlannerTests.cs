namespace Plugin.Maui.SmartUpload.Tests;

public sealed class ChunkPlannerTests
{
	[Fact]
	public void GetCount_RoundsUp()
	{
		Assert.Equal(3, ChunkPlanner.GetCount(2500, 1000));
		Assert.Equal(1, ChunkPlanner.GetCount(100, 1000));
		Assert.Equal(0, ChunkPlanner.GetCount(0, 1000));
	}

	[Fact]
	public void Plan_FromZero_CoversEntireFile()
	{
		var chunks = ChunkPlanner.Plan(2500, 1000, 0).ToList();

		Assert.Equal(3, chunks.Count);
		Assert.Equal(0, chunks[0].Offset);
		Assert.Equal(1000, chunks[0].Length);
		Assert.Equal(1000, chunks[1].Offset);
		Assert.Equal(2000, chunks[2].Offset);
		Assert.Equal(500, chunks[2].Length);
		Assert.Equal(3, chunks[0].TotalChunks);
	}

	[Fact]
	public void Plan_FromOffset_SkipsCompletedBytes()
	{
		var chunks = ChunkPlanner.Plan(2500, 1000, 1000).ToList();

		Assert.Equal(2, chunks.Count);
		Assert.Equal(1000, chunks[0].Offset);
		Assert.Equal(1, chunks[0].Index);
	}

	[Fact]
	public void Plan_UnalignedOffset_SendsRemainder()
	{
		var chunks = ChunkPlanner.Plan(2500, 1000, 1500).ToList();

		var chunk = Assert.Single(chunks);
		Assert.Equal(1500, chunk.Offset);
		Assert.Equal(1000, chunk.Length);
	}
}
