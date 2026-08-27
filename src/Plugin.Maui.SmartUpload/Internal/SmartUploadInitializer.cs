using Microsoft.Extensions.DependencyInjection;

namespace Plugin.Maui.SmartUpload;

sealed class SmartUploadInitializer : IMauiInitializeService
{
	public void Initialize(IServiceProvider services)
	{
		var options = services.GetRequiredService<SmartUploadOptions>();
		var client = services.GetRequiredService<ISmartUploadClient>();

		if (options.EnableLogging)
			client.EnableLogging(true, options.Logger ?? MauiAppBuilderExtensions.CreateLoggerAdapter(services));

		if (options.ResumeInterruptedOnStart)
			_ = ResumeSafeAsync(client);
	}

	static async Task ResumeSafeAsync(ISmartUploadClient client)
	{
		try
		{
			await client.ResumeInterruptedAsync().ConfigureAwait(false);
		}
		catch
		{
			// Startup resume must not crash the host app.
		}
	}
}
