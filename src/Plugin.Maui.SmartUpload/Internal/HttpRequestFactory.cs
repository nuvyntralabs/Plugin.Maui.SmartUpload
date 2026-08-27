namespace Plugin.Maui.SmartUpload;

static class HttpRequestFactory
{
	public static void ApplyHeaders(HttpRequestMessage request, IDictionary<string, string> headers)
	{
		foreach (var pair in headers)
		{
			if (string.IsNullOrWhiteSpace(pair.Key))
				continue;

			if (!request.Headers.TryAddWithoutValidation(pair.Key, pair.Value) && request.Content is not null)
				request.Content.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
		}
	}

	public static async Task<HttpResponseMessage> SendAsync(
		HttpClient http,
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		try
		{
			return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
		}
		catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
		{
			throw new SmartUploadException(UploadError.Timeout, "The upload request timed out.", ex);
		}
		catch (HttpRequestException ex)
		{
			throw new SmartUploadException(UploadError.Network, ex.Message, ex);
		}
		catch (IOException ex)
		{
			throw new SmartUploadException(UploadError.Network, ex.Message, ex);
		}
	}

	public static void EnsureSuccess(HttpResponseMessage response, params int[] extraSuccessCodes)
	{
		var code = (int)response.StatusCode;
		if (code is >= 200 and <= 299 || extraSuccessCodes.Contains(code))
			return;

		var retryable = code is 408 or 429 or >= 500;
		throw new SmartUploadException(
			UploadError.HttpFailure,
			$"Upload request failed with HTTP {code}.",
			statusCode: code,
			isRetryable: retryable);
	}

	public static Uri ResolveLocation(Uri requestUri, HttpResponseMessage response)
	{
		var location = response.Headers.Location;
		if (location is null)
			throw new SmartUploadException(UploadError.Protocol, "The server did not return a Location header.");

		return location.IsAbsoluteUri ? location : new Uri(requestUri, location);
	}
}
