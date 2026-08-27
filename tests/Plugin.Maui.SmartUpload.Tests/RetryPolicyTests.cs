namespace Plugin.Maui.SmartUpload.Tests;

public sealed class RetryPolicyTests
{
	[Fact]
	public void ShouldRetry_RespectsMaxRetries()
	{
		var policy = new RetryPolicy { MaxRetries = 2 };

		Assert.True(policy.ShouldRetry(new HttpRequestException("down"), failedAttempts: 0));
		Assert.True(policy.ShouldRetry(new HttpRequestException("down"), failedAttempts: 1));
		Assert.False(policy.ShouldRetry(new HttpRequestException("down"), failedAttempts: 2));
	}

	[Fact]
	public void ShouldRetry_RejectsCancellation()
	{
		Assert.False(RetryPolicy.Default.ShouldRetry(new OperationCanceledException(), 0));
	}

	[Fact]
	public void ShouldRetry_UsesExceptionFlag()
	{
		var retryable = new SmartUploadException(UploadError.HttpFailure, "503", statusCode: 503);
		var fatal = new SmartUploadException(UploadError.HttpFailure, "401", statusCode: 401);

		Assert.True(RetryPolicy.Default.ShouldRetry(retryable, 0));
		Assert.False(RetryPolicy.Default.ShouldRetry(fatal, 0));
	}

	[Fact]
	public void GetDelay_GrowsAndCaps()
	{
		var policy = new RetryPolicy
		{
			InitialDelay = TimeSpan.FromMilliseconds(100),
			MaxDelay = TimeSpan.FromMilliseconds(250),
			BackoffMultiplier = 3,
			UseJitter = false
		};

		Assert.Equal(100, policy.GetDelay(1).TotalMilliseconds);
		Assert.Equal(250, policy.GetDelay(3).TotalMilliseconds);
	}

	[Fact]
	public void None_NeverRetries()
	{
		Assert.False(RetryPolicy.None.ShouldRetry(new HttpRequestException("x"), 0));
	}
}
