using Microsoft.Extensions.Logging;
using Plugin.Maui.SmartUpload;

namespace SmartUpload.Sample;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseSmartUpload(options =>
			{
				options.EnableLogging = true;
				options.DefaultChunkSize = 256 * 1024;
				options.MaxConcurrentUploads = 2;
				options.ResumeInterruptedOnStart = true;
				options.DefaultRetry = new RetryPolicy
				{
					MaxRetries = 5,
					InitialDelay = TimeSpan.FromSeconds(1),
					MaxDelay = TimeSpan.FromSeconds(15)
				};
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
