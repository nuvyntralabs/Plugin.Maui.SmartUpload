namespace Plugin.Maui.SmartUpload.Tests;

public sealed class ProtocolAndStoreTests
{
	[Fact]
	public void ContentRange_ParsesRangeHeader()
	{
		Assert.True(ContentRangeProtocol.TryParseRangeEnd("bytes=0-1023", out var end));
		Assert.Equal(1023, end);
		Assert.True(ContentRangeProtocol.TryParseRangeEnd("0-2047", out end));
		Assert.Equal(2047, end);
	}

	[Fact]
	public void Tus_EncodesMetadata()
	{
		var context = new UploadSessionContext
		{
			SessionId = "abc",
			Endpoint = new Uri("https://example.com/files"),
			FilePath = "/tmp/a.bin",
			FileName = "photo.jpg",
			FileSize = 10,
			ContentType = "image/jpeg",
			Method = HttpMethod.Put,
			ChunkSize = 1024,
			Metadata = new Dictionary<string, string> { ["album"] = "summer" }
		};

		var metadata = TusProtocol.BuildMetadata(context);
		Assert.Contains("filename ", metadata);
		Assert.Contains("contentType ", metadata);
		Assert.Contains("album ", metadata);
	}

	[Fact]
	public async Task FileStore_RoundTripsSession()
	{
		var directory = Path.Combine(Path.GetTempPath(), "smartupload-store-" + Guid.NewGuid().ToString("N"));
		try
		{
			var store = new FileUploadStore(directory);
			var record = new UploadSessionRecord
			{
				SessionId = "session1",
				FilePath = "/tmp/file.bin",
				FileName = "file.bin",
				FileSize = 2048,
				Endpoint = "https://example.com/upload",
				BytesUploaded = 512,
				State = UploadState.Paused,
				Protocol = UploadProtocolKind.Tus,
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow
			};

			await store.SaveAsync(record);
			var loaded = await store.GetAsync("session1");
			Assert.NotNull(loaded);
			Assert.Equal(512, loaded!.BytesUploaded);
			Assert.Equal(UploadState.Paused, loaded.State);

			var all = await store.GetAllAsync();
			Assert.Single(all);

			await store.DeleteAsync("session1");
			Assert.Null(await store.GetAsync("session1"));
		}
		finally
		{
			if (Directory.Exists(directory))
				Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public void SessionSerializer_PreservesEnumsAndRetry()
	{
		var record = new UploadSessionRecord
		{
			SessionId = "s1",
			FilePath = "/a",
			FileName = "a",
			Endpoint = "https://example.com/x",
			State = UploadState.Failed,
			Error = UploadError.Network,
			Protocol = UploadProtocolKind.ContentRange,
			Retry = new RetryPolicy { MaxRetries = 3, InitialDelay = TimeSpan.FromMilliseconds(50) }.ToData()
		};

		var clone = SessionSerializer.Deserialize(SessionSerializer.Serialize(record));
		Assert.NotNull(clone);
		Assert.Equal(UploadState.Failed, clone!.State);
		Assert.Equal(UploadError.Network, clone.Error);
		Assert.Equal(3, clone.Retry.MaxRetries);
	}
}
