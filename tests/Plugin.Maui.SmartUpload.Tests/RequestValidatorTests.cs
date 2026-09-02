namespace Plugin.Maui.SmartUpload.Tests;

public sealed class RequestValidatorTests
{
	[Fact]
	public void Validate_RejectsMissingFile()
	{
		var request = new UploadRequest
		{
			FilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
			Endpoint = new Uri("https://example.com/upload")
		};

		var ex = Assert.Throws<SmartUploadException>(() => RequestValidator.Validate(request, new SmartUploadOptions()));
		Assert.Equal(UploadError.FileNotFound, ex.Error);
	}

	[Fact]
	public void Validate_RejectsNonHttpEndpoint()
	{
		var path = WriteTempFile("hello");
		try
		{
			var request = new UploadRequest
			{
				FilePath = path,
				Endpoint = new Uri("ftp://example.com/upload")
			};

			var ex = Assert.Throws<SmartUploadException>(() => RequestValidator.Validate(request, new SmartUploadOptions()));
			Assert.Equal(UploadError.InvalidRequest, ex.Error);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void Validate_RejectsHttp_WhenHttpsRequired()
	{
		var path = WriteTempFile("hello");
		try
		{
			var request = new UploadRequest
			{
				FilePath = path,
				Endpoint = new Uri("http://example.com/upload")
			};

			var ex = Assert.Throws<SmartUploadException>(() => RequestValidator.Validate(request, new SmartUploadOptions()));
			Assert.Equal(UploadError.InvalidRequest, ex.Error);

			RequestValidator.Validate(request, new SmartUploadOptions { RequireHttps = false });
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void ValidateSessionId_RejectsIllegalCharacters()
	{
		Assert.Throws<SmartUploadException>(() => RequestValidator.ValidateSessionId("bad id"));
		Assert.Throws<SmartUploadException>(() => RequestValidator.ValidateSessionId("../x"));
		RequestValidator.ValidateSessionId("ok-id_1");
	}

	[Fact]
	public void EnsureFileUnchanged_DetectsRewrite()
	{
		var path = WriteTempFile("one");
		try
		{
			var info = new FileInfo(path);
			var record = new UploadSessionRecord
			{
				FilePath = path,
				FileSize = info.Length,
				FileLastWriteTimeUtcTicks = info.LastWriteTimeUtc.Ticks
			};

			File.WriteAllText(path, "changed-content");
			var ex = Assert.Throws<SmartUploadException>(() => RequestValidator.EnsureFileUnchanged(record));
			Assert.Equal(UploadError.FileChanged, ex.Error);
		}
		finally
		{
			File.Delete(path);
		}
	}

	static string WriteTempFile(string contents)
	{
		var path = Path.Combine(Path.GetTempPath(), "smartupload-test-" + Guid.NewGuid().ToString("N") + ".txt");
		File.WriteAllText(path, contents);
		return path;
	}
}
