namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Classifies why an upload failed or why an API call was rejected.
/// </summary>
public enum UploadError
{
	None,
	InvalidRequest,
	FileNotFound,
	FileChanged,
	Network,
	Timeout,
	HttpFailure,
	Cancelled,
	Protocol,
	Persistence,
	Conflict,
	Unknown
}
