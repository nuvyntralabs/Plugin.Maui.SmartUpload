namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Defaults applied to every session created through this client.
/// </summary>
public sealed class SmartUploadOptions
{
	public const int MinimumChunkSize = 64 * 1024;
	public const int DefaultChunkSizeBytes = 1024 * 1024;
	public const int MaximumChunkSize = 32 * 1024 * 1024;

	/// <summary>
	/// Default slice size in bytes (1 MB). Clamped to <see cref="MinimumChunkSize"/>..<see cref="MaximumChunkSize"/>.
	/// </summary>
	public int DefaultChunkSize { get; set; } = DefaultChunkSizeBytes;

	/// <summary>
	/// Maximum number of sessions transferring at once. Additional sessions stay queued.
	/// </summary>
	public int MaxConcurrentUploads { get; set; } = 2;

	/// <summary>
	/// Retry policy used when a request does not set its own.
	/// </summary>
	public RetryPolicy DefaultRetry { get; set; } = RetryPolicy.Default;

	/// <summary>
	/// Per-request HTTP timeout (one chunk).
	/// </summary>
	public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(100);

	/// <summary>
	/// When <see langword="true"/>, <c>UseSmartUpload</c> resumes interrupted sessions at startup.
	/// </summary>
	public bool ResumeInterruptedOnStart { get; set; }

	/// <summary>
	/// When <see langword="true"/>, tus DELETE is sent if the server advertised the termination extension.
	/// </summary>
	public bool DeleteRemoteOnCancel { get; set; }

	public bool EnableLogging { get; set; }

	/// <summary>
	/// Optional directory for the default file store. Defaults to app data.
	/// </summary>
	public string? StorageDirectory { get; set; }

	/// <summary>
	/// Optional persistence implementation. When omitted, sessions are stored as JSON files.
	/// </summary>
	public IUploadStore? Store { get; set; }

	/// <summary>
	/// Optional shared <see cref="System.Net.Http.HttpClient"/>. The plugin does not dispose a caller-supplied instance.
	/// </summary>
	public HttpClient? HttpClient { get; set; }

	/// <summary>
	/// Protocol used when a request sets <see cref="UploadProtocolKind.Custom"/>.
	/// </summary>
	public IUploadProtocol? CustomProtocol { get; set; }

	public ISmartUploadLogger? Logger { get; set; }

	internal int ResolveChunkSize(int? requested)
	{
		if (requested is int explicitSize)
		{
			if (explicitSize < 1)
				throw new SmartUploadException(UploadError.InvalidRequest, "ChunkSize must be positive.");
			return Math.Min(explicitSize, MaximumChunkSize);
		}

		var size = DefaultChunkSize < MinimumChunkSize ? MinimumChunkSize : DefaultChunkSize;
		return size > MaximumChunkSize ? MaximumChunkSize : size;
	}

	internal int ResolveMaxConcurrency() => MaxConcurrentUploads < 1 ? 1 : MaxConcurrentUploads;
}
