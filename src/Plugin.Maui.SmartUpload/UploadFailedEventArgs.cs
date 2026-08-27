namespace Plugin.Maui.SmartUpload;

public sealed class UploadFailedEventArgs : EventArgs
{
	public required UploadSession Session { get; init; }

	public required UploadError Error { get; init; }

	public string? Message { get; init; }

	public Exception? Exception { get; init; }
}
