namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Entry point for the SmartUpload plugin when dependency injection is not used.
/// </summary>
public static class SmartUpload
{
	static ISmartUploadClient? _current;

	/// <summary>
	/// Gets the shared <see cref="ISmartUploadClient"/> instance.
	/// </summary>
	public static ISmartUploadClient Current => _current ??= SmartUploadClient.Create(new SmartUploadOptions());

	/// <summary>
	/// Replaces the shared instance. Intended for tests and custom implementations.
	/// </summary>
	public static void SetDefault(ISmartUploadClient implementation) =>
		_current = implementation ?? throw new ArgumentNullException(nameof(implementation));

	/// <summary>
	/// Creates an isolated client. Use this in tests or when you need a private store / <see cref="HttpClient"/>.
	/// </summary>
	public static ISmartUploadClient Create(SmartUploadOptions? options = null) =>
		SmartUploadClient.Create(options ?? new SmartUploadOptions());
}
