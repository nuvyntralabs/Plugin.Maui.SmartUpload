namespace Plugin.Maui.SmartUpload;

public sealed class UploadProgressEventArgs : EventArgs
{
	public required UploadSession Session { get; init; }

	public required UploadProgress Progress { get; init; }
}
