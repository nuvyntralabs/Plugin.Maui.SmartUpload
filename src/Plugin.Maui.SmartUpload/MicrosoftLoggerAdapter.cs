using Microsoft.Extensions.Logging;

namespace Plugin.Maui.SmartUpload;

sealed class MicrosoftLoggerAdapter(ILogger logger) : ISmartUploadLogger
{
	public void Log(SmartUploadLogLevel level, string message, Exception? exception = null)
	{
		logger.Log(ToLogLevel(level), exception, "{Message}", message);
	}

	static LogLevel ToLogLevel(SmartUploadLogLevel level) => level switch
	{
		SmartUploadLogLevel.Trace => LogLevel.Trace,
		SmartUploadLogLevel.Debug => LogLevel.Debug,
		SmartUploadLogLevel.Information => LogLevel.Information,
		SmartUploadLogLevel.Warning => LogLevel.Warning,
		SmartUploadLogLevel.Error => LogLevel.Error,
		_ => LogLevel.Information
	};
}
