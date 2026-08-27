using System.Net;
using System.Text;

namespace Plugin.Maui.SmartUpload.Tests;

public sealed class UploadClientTests
{
	[Fact]
	public async Task ContentRange_UploadsAllChunks_AndPersistsCompletion()
	{
		var file = WriteBytes(Enumerable.Range(0, 2500).Select(i => (byte)(i % 256)).ToArray());
		var received = new List<byte[]>();
		var handler = new RecordingHandler((_, body) =>
		{
			received.Add(body);
			return Ok();
		});

		using var client = CreateClient(handler, out var store);
		var session = await client.EnqueueAsync(new UploadRequest
		{
			FilePath = file,
			Endpoint = new Uri("https://example.com/upload"),
			ChunkSize = 1000,
			Retry = RetryPolicy.None
		});

		await WaitForStateAsync(client, session.SessionId, UploadState.Completed);

		Assert.Equal(3, handler.Requests.Count(r => r.Method == HttpMethod.Put));
		Assert.Equal(2500, received.Sum(b => b.Length));
		Assert.Contains(handler.Requests, r => r.ContentRange?.Contains("bytes 0-999/2500") == true);

		var persisted = await store.GetAsync(session.SessionId);
		Assert.Equal(UploadState.Completed, persisted!.State);
		Assert.Equal(2500, persisted.BytesUploaded);
	}

	[Fact]
	public async Task Retry_ReplaysFailedChunk()
	{
		var file = WriteBytes(new byte[800]);
		var handler = new RecordingHandler((_, _) => Ok()) { FailNext = 2 };

		using var client = CreateClient(handler, out _);
		var session = await client.EnqueueAsync(new UploadRequest
		{
			FilePath = file,
			Endpoint = new Uri("https://example.com/upload"),
			ChunkSize = 1000,
			Retry = new RetryPolicy
			{
				MaxRetries = 3,
				InitialDelay = TimeSpan.FromMilliseconds(5),
				MaxDelay = TimeSpan.FromMilliseconds(5),
				UseJitter = false
			}
		});

		await WaitForStateAsync(client, session.SessionId, UploadState.Completed);
		Assert.True(handler.Requests.Count(r => r.Method == HttpMethod.Put) >= 3);
	}

	[Fact]
	public async Task PauseAndResume_ContinuesFromPersistedOffset()
	{
		var file = WriteBytes(new byte[4000]);
		var firstChunk = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var putCount = 0;
		var handler = new RecordingHandler((request, _) =>
		{
			if (request.Method == HttpMethod.Put)
			{
				var current = Interlocked.Increment(ref putCount);
				if (current == 1)
					firstChunk.TrySetResult();
			}

			return Ok();
		})
		{
			Delay = TimeSpan.FromMilliseconds(80)
		};

		using var client = CreateClient(handler, out var store);
		var session = await client.EnqueueAsync(new UploadRequest
		{
			FilePath = file,
			Endpoint = new Uri("https://example.com/upload"),
			ChunkSize = 1000,
			Retry = RetryPolicy.None
		});

		await firstChunk.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await client.PauseAsync(session.SessionId);

		var paused = await store.GetAsync(session.SessionId);
		Assert.Equal(UploadState.Paused, paused!.State);
		Assert.True(paused.BytesUploaded < 4000);
		Assert.True(paused.BytesUploaded >= 0);

		var putsAfterPause = handler.Requests.Count(r => r.Method == HttpMethod.Put);
		await client.ResumeAsync(session.SessionId);
		await WaitForStateAsync(client, session.SessionId, UploadState.Completed);

		Assert.True(handler.Requests.Count(r => r.Method == HttpMethod.Put) >= putsAfterPause);
		var completed = await store.GetAsync(session.SessionId);
		Assert.Equal(4000, completed!.BytesUploaded);
	}

	[Fact]
	public async Task NewClient_ResumesPersistedSession()
	{
		var file = WriteBytes(new byte[2000]);
		var directory = Path.Combine(Path.GetTempPath(), "smartupload-resume-" + Guid.NewGuid().ToString("N"));
		var handler = new RecordingHandler((_, _) => Ok());
		var store = new FileUploadStore(directory);

		try
		{
			using (var first = CreateClient(handler, store))
			{
				var session = await first.EnqueueAsync(new UploadRequest
				{
					SessionId = "persist1",
					FilePath = file,
					Endpoint = new Uri("https://example.com/upload"),
					ChunkSize = 1000,
					AutoStart = false,
					Retry = RetryPolicy.None
				});

				var record = await store.GetAsync(session.SessionId);
				record!.BytesUploaded = 1000;
				record.RemoteOffset = 1000;
				record.State = UploadState.Paused;
				await store.SaveAsync(record);
			}

			handler.Requests.Clear();
			using var second = CreateClient(handler, store);
			await second.ResumeAsync("persist1");
			await WaitForStateAsync(second, "persist1", UploadState.Completed);

			Assert.Single(handler.Requests, r => r.Method == HttpMethod.Put);
			Assert.Contains(handler.Requests, r => r.ContentRange?.Contains("bytes 1000-1999/2000") == true);
		}
		finally
		{
			if (Directory.Exists(directory))
				Directory.Delete(directory, recursive: true);
			File.Delete(file);
		}
	}

	[Fact]
	public async Task Tus_CreatesThenPatchesChunks()
	{
		var file = WriteBytes(new byte[1500]);
		var offset = 0L;
		var handler = new RecordingHandler((request, body) =>
		{
			if (request.Method == HttpMethod.Post)
			{
				var response = new HttpResponseMessage(HttpStatusCode.Created);
				response.Headers.Location = new Uri("https://example.com/files/abc");
				response.Headers.TryAddWithoutValidation("Tus-Resumable", "1.0.0");
				return response;
			}

			if (request.Method == HttpMethod.Head)
			{
				var head = new HttpResponseMessage(HttpStatusCode.OK);
				head.Headers.TryAddWithoutValidation("Tus-Resumable", "1.0.0");
				head.Headers.TryAddWithoutValidation("Upload-Offset", offset.ToString());
				head.Headers.TryAddWithoutValidation("Upload-Length", "1500");
				return head;
			}

			if (request.Method == HttpMethod.Patch)
			{
				offset += body.Length;
				var patch = new HttpResponseMessage(HttpStatusCode.NoContent);
				patch.Headers.TryAddWithoutValidation("Tus-Resumable", "1.0.0");
				patch.Headers.TryAddWithoutValidation("Upload-Offset", offset.ToString());
				return patch;
			}

			return new HttpResponseMessage(HttpStatusCode.NotFound);
		});

		using var client = CreateClient(handler, out _);
		var session = await client.EnqueueAsync(new UploadRequest
		{
			FilePath = file,
			Endpoint = new Uri("https://example.com/files"),
			Protocol = UploadProtocolKind.Tus,
			ChunkSize = 1000,
			Retry = RetryPolicy.None
		});

		await WaitForStateAsync(client, session.SessionId, UploadState.Completed);
		Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post);
		Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Head);
		Assert.Equal(2, handler.Requests.Count(r => r.Method == HttpMethod.Patch));
	}

	[Fact]
	public async Task Cancel_MarksSessionCancelled()
	{
		var file = WriteBytes(new byte[3000]);
		var handler = new RecordingHandler((_, _) => Ok()) { Delay = TimeSpan.FromMilliseconds(200) };

		using var client = CreateClient(handler, out var store);
		var session = await client.EnqueueAsync(new UploadRequest
		{
			FilePath = file,
			Endpoint = new Uri("https://example.com/upload"),
			ChunkSize = 1000,
			Retry = RetryPolicy.None
		});

		await Task.Delay(50);
		await client.CancelAsync(session.SessionId);

		var record = await store.GetAsync(session.SessionId);
		Assert.Equal(UploadState.Cancelled, record!.State);
	}

	static SmartUploadClient CreateClient(RecordingHandler handler, out MemoryUploadStore store)
	{
		store = new MemoryUploadStore();
		return CreateClient(handler, store);
	}

	static SmartUploadClient CreateClient(RecordingHandler handler, IUploadStore store)
	{
		var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
		return SmartUploadClient.Create(new SmartUploadOptions
		{
			HttpClient = http,
			Store = store,
			DefaultChunkSize = 1000,
			MaxConcurrentUploads = 1,
			DefaultRetry = RetryPolicy.None,
			EnableLogging = false
		});
	}

	static async Task WaitForStateAsync(ISmartUploadClient client, string sessionId, UploadState expected)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
		while (DateTime.UtcNow < deadline)
		{
			var session = await client.GetSessionAsync(sessionId);
			if (session?.State == expected)
				return;
			if (session?.State == UploadState.Failed)
				throw new InvalidOperationException($"Session failed: {session.LastError}");
			await Task.Delay(20);
		}

		var last = await client.GetSessionAsync(sessionId);
		throw new TimeoutException($"Timed out waiting for {expected}. Last state: {last?.State} ({last?.LastError})");
	}

	static HttpResponseMessage Ok() => new(HttpStatusCode.OK)
	{
		Content = new StringContent("ok", Encoding.UTF8, "text/plain")
	};

	static string WriteBytes(byte[] bytes)
	{
		var path = Path.Combine(Path.GetTempPath(), "smartupload-" + Guid.NewGuid().ToString("N") + ".bin");
		File.WriteAllBytes(path, bytes);
		return path;
	}
}
