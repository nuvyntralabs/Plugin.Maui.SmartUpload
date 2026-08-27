using System.Diagnostics;

namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Writes plugin diagnostics to <see cref="Debug.WriteLine(string?)"/>.
/// </summary>
public sealed class DebugSmartUploadLogger : ISmartUploadLogger
{
	public void Log(SmartUploadLogLevel level, string message, Exception? exception = null)
	{
		var line = exception is null
			? $"[SmartUpload] {level}: {message}"
			: $"[SmartUpload] {level}: {message}{Environment.NewLine}{exception}";

		Debug.WriteLine(line);
	}
}
