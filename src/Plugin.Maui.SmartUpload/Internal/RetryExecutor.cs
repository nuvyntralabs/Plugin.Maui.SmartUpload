namespace Plugin.Maui.SmartUpload;

static class RetryExecutor
{
	public static async Task ExecuteAsync(
		Func<CancellationToken, Task> action,
		RetryPolicy policy,
		Action<int, Exception, TimeSpan>? onRetry,
		CancellationToken cancellationToken)
	{
		var failedAttempts = 0;

		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				await action(cancellationToken).ConfigureAwait(false);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex) when (policy.ShouldRetry(ex, failedAttempts))
			{
				failedAttempts++;
				var delay = policy.GetDelay(failedAttempts);
				onRetry?.Invoke(failedAttempts, ex, delay);
				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
