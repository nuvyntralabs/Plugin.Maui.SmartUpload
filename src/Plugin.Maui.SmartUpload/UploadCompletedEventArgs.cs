namespace Plugin.Maui.SmartUpload;

public sealed class UploadCompletedEventArgs : EventArgs
{
	public required UploadSession Session { get; init; }
}
