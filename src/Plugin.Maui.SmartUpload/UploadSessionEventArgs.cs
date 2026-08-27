namespace Plugin.Maui.SmartUpload;

public sealed class UploadSessionEventArgs : EventArgs
{
	public required UploadSession Session { get; init; }
}
