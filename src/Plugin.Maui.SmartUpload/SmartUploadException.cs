namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Error raised by the SmartUpload plugin or by a protocol implementation.
/// </summary>
public sealed class SmartUploadException : Exception
{
	public SmartUploadException(UploadError error, string message, Exception? inner = null, int? statusCode = null, bool? isRetryable = null)
		: base(message, inner)
	{
		Error = error;
		StatusCode = statusCode;
		IsRetryable = isRetryable ?? InferRetryable(error, statusCode, inner);
	}

	public UploadError Error { get; }

	public int? StatusCode { get; }

	public bool IsRetryable { get; }

	static bool InferRetryable(UploadError error, int? statusCode, Exception? inner)
	{
		if (error is UploadError.Cancelled or UploadError.InvalidRequest or UploadError.FileNotFound or UploadError.FileChanged or UploadError.Conflict)
			return false;

		if (statusCode is int code)
			return code is 408 or 429 or >= 500;

		return inner is HttpRequestException or IOException or TimeoutException
			|| error is UploadError.Network or UploadError.Timeout;
	}
}
