namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Exponential backoff used when a chunk transfer fails with a retryable error.
/// </summary>
public sealed class RetryPolicy
{
	static readonly int[] DefaultStatusCodes = [408, 429, 500, 502, 503, 504];

	/// <summary>
	/// Maximum number of retries after the first attempt. <c>0</c> disables retry.
	/// </summary>
	public int MaxRetries { get; init; } = 5;

	/// <summary>
	/// Delay before the first retry.
	/// </summary>
	public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Upper bound for the computed delay.
	/// </summary>
	public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Multiplier applied to the previous delay after each failed attempt.
	/// </summary>
	public double BackoffMultiplier { get; init; } = 2d;

	/// <summary>
	/// When <see langword="true"/>, the delay is jittered between 80% and 120%.
	/// </summary>
	public bool UseJitter { get; init; } = true;

	/// <summary>
	/// HTTP status codes that should be retried. Defaults to 408, 429, and 5xx gateway errors.
	/// </summary>
	public IReadOnlySet<int> RetryableStatusCodes { get; init; } = new HashSet<int>(DefaultStatusCodes);

	/// <summary>
	/// A policy with five retries and exponential backoff.
	/// </summary>
	public static RetryPolicy Default => new();

	/// <summary>
	/// A policy that never retries.
	/// </summary>
	public static RetryPolicy None => new() { MaxRetries = 0 };

	/// <summary>
	/// Returns <see langword="true"/> when another attempt should be made.
	/// </summary>
	public bool ShouldRetry(Exception exception, int failedAttempts)
	{
		if (failedAttempts >= MaxRetries)
			return false;

		if (exception is OperationCanceledException)
			return false;

		if (exception is SmartUploadException upload)
			return upload.IsRetryable;

		return exception is HttpRequestException or IOException or TimeoutException or TaskCanceledException;
	}

	/// <summary>
	/// Computes the wait before retry number <paramref name="attempt"/> (1-based).
	/// </summary>
	public TimeSpan GetDelay(int attempt)
	{
		if (attempt < 1)
			attempt = 1;

		var milliseconds = InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attempt - 1);
		milliseconds = Math.Min(milliseconds, MaxDelay.TotalMilliseconds);

		if (UseJitter)
			milliseconds *= Random.Shared.NextDouble() * 0.4 + 0.8;

		return TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
	}

	internal RetryPolicyData ToData() => new()
	{
		MaxRetries = MaxRetries,
		InitialDelayMs = InitialDelay.TotalMilliseconds,
		MaxDelayMs = MaxDelay.TotalMilliseconds,
		BackoffMultiplier = BackoffMultiplier,
		UseJitter = UseJitter,
		RetryableStatusCodes = RetryableStatusCodes.ToArray()
	};

	internal static RetryPolicy FromData(RetryPolicyData? data)
	{
		if (data is null)
			return Default;

		return new RetryPolicy
		{
			MaxRetries = data.MaxRetries,
			InitialDelay = TimeSpan.FromMilliseconds(data.InitialDelayMs),
			MaxDelay = TimeSpan.FromMilliseconds(data.MaxDelayMs),
			BackoffMultiplier = data.BackoffMultiplier,
			UseJitter = data.UseJitter,
			RetryableStatusCodes = data.RetryableStatusCodes is { Length: > 0 }
				? new HashSet<int>(data.RetryableStatusCodes)
				: new HashSet<int>(DefaultStatusCodes)
		};
	}
}
