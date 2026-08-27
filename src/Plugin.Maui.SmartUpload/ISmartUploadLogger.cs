namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Receives diagnostic messages from the SmartUpload plugin.
/// </summary>
public interface ISmartUploadLogger
{
	void Log(SmartUploadLogLevel level, string message, Exception? exception = null);
}
