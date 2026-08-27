namespace Plugin.Maui.SmartUpload;

/// <summary>
/// Built-in wire protocols for chunked uploads.
/// </summary>
public enum UploadProtocolKind
{
	/// <summary>
	/// Sends each chunk with an HTTP <c>Content-Range</c> header
	/// (<c>bytes start-end/total</c>) plus <c>X-Upload-Id</c>.
	/// </summary>
	ContentRange,

	/// <summary>
	/// <see href="https://tus.io/protocols/resumable-upload.html">tus 1.0</see>
	/// creation, <c>HEAD</c> offset query, and <c>PATCH</c> chunks.
	/// </summary>
	Tus,

	/// <summary>
	/// Uses <see cref="SmartUploadOptions.CustomProtocol"/> or
	/// <see cref="UploadRequest.CustomProtocol"/>.
	/// </summary>
	Custom
}
