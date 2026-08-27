using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;

namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Registers the SmartUpload plugin with the MAUI dependency injection container.
/// </summary>
public static class MauiAppBuilderExtensions
{
	/// <summary>
	/// Adds <see cref="ISmartUploadClient"/> as a singleton.
	/// </summary>
	/// <example>
	/// <code>
	/// builder.UseSmartUpload(options =>
	/// {
	///     options.EnableLogging = true;
	///     options.DefaultChunkSize = 512 * 1024;
	///     options.ResumeInterruptedOnStart = true;
	/// });
	/// </code>
	/// </example>
	public static MauiAppBuilder UseSmartUpload(this MauiAppBuilder builder, Action<SmartUploadOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var options = new SmartUploadOptions();
		configure?.Invoke(options);

		builder.Services.AddSingleton(options);
		builder.Services.AddSingleton<ISmartUploadClient>(services =>
		{
			options.Logger ??= CreateLoggerAdapter(services);
			var client = SmartUpload.Create(options);
			SmartUpload.SetDefault(client);
			return client;
		});
		builder.Services.AddTransient<IMauiInitializeService, SmartUploadInitializer>();

		return builder;
	}

	internal static ISmartUploadLogger? CreateLoggerAdapter(IServiceProvider serviceProvider)
	{
		var factory = serviceProvider.GetService<ILoggerFactory>();
		return factory is null ? null : new MicrosoftLoggerAdapter(factory.CreateLogger("Plugin.Maui.SmartUpload"));
	}
}
