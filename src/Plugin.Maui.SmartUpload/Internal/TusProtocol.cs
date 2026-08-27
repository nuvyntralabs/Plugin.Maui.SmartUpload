using System.Net.Http.Headers;
using System.Text;

namespace Plugin.Maui.SmartUpload;

sealed class TusProtocol(HttpClient http, SmartUploadOptions options) : IUploadProtocol
{
	public const string TusResumable = "1.0.0";
	public const string LocationKey = "location";
	public const string ExtensionsKey = "tusExtensions";

	public UploadProtocolKind Kind => UploadProtocolKind.Tus;

	public async Task InitializeAsync(UploadSessionContext context, CancellationToken cancellationToken)
	{
		if (context.ProtocolState.TryGetValue(LocationKey, out var existing) && !string.IsNullOrWhiteSpace(existing))
		{
			context.Endpoint = new Uri(existing, UriKind.Absolute);
			context.RemoteUploadId ??= existing;
			return;
		}

		using var request = new HttpRequestMessage(HttpMethod.Post, context.Endpoint);
		request.Headers.TryAddWithoutValidation("Tus-Resumable", TusResumable);
		request.Headers.TryAddWithoutValidation("Upload-Length", context.FileSize.ToString());
		request.Headers.TryAddWithoutValidation("Upload-Metadata", BuildMetadata(context));
		HttpRequestFactory.ApplyHeaders(request, context.Headers);

		using var response = await HttpRequestFactory.SendAsync(http, request, cancellationToken).ConfigureAwait(false);
		HttpRequestFactory.EnsureSuccess(response, extraSuccessCodes: [201]);

		var location = HttpRequestFactory.ResolveLocation(context.Endpoint, response);
		context.Endpoint = location;
		context.RemoteUploadId = location.ToString();
		context.RemoteOffset = 0;
		context.ProtocolState[LocationKey] = location.ToString();

		if (response.Headers.TryGetValues("Tus-Extension", out var extensions))
			context.ProtocolState[ExtensionsKey] = string.Join(",", extensions);
	}

	public async Task QueryRemoteProgressAsync(UploadSessionContext context, CancellationToken cancellationToken)
	{
		if (!context.ProtocolState.TryGetValue(LocationKey, out var location) || string.IsNullOrWhiteSpace(location))
			return;

		context.Endpoint = new Uri(location, UriKind.Absolute);

		using var request = new HttpRequestMessage(HttpMethod.Head, context.Endpoint);
		request.Headers.TryAddWithoutValidation("Tus-Resumable", TusResumable);
		request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
		HttpRequestFactory.ApplyHeaders(request, context.Headers);

		using var response = await HttpRequestFactory.SendAsync(http, request, cancellationToken).ConfigureAwait(false);
		if ((int)response.StatusCode == 404)
			throw new SmartUploadException(UploadError.Protocol, "The remote tus upload no longer exists.", statusCode: 404, isRetryable: false);

		HttpRequestFactory.EnsureSuccess(response);

		if (response.Headers.TryGetValues("Upload-Offset", out var offsets)
			&& long.TryParse(offsets.FirstOrDefault(), out var offset))
		{
			context.RemoteOffset = offset;
		}
	}

	public async Task UploadChunkAsync(UploadSessionContext context, ChunkInfo chunk, Stream chunkStream, CancellationToken cancellationToken)
	{
		using var content = new StreamContent(chunkStream, 64 * 1024);
		content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
		content.Headers.ContentLength = chunk.Length;

		using var request = new HttpRequestMessage(HttpMethod.Patch, context.Endpoint)
		{
			Content = content
		};
		request.Headers.TryAddWithoutValidation("Tus-Resumable", TusResumable);
		request.Headers.TryAddWithoutValidation("Upload-Offset", chunk.Offset.ToString());
		HttpRequestFactory.ApplyHeaders(request, context.Headers);

		using var response = await HttpRequestFactory.SendAsync(http, request, cancellationToken).ConfigureAwait(false);

		if ((int)response.StatusCode == 409)
			throw new SmartUploadException(UploadError.Conflict, "tus Upload-Offset does not match the server.", statusCode: 409, isRetryable: false);

		HttpRequestFactory.EnsureSuccess(response);

		if (response.Headers.TryGetValues("Upload-Offset", out var offsets)
			&& long.TryParse(offsets.FirstOrDefault(), out var offset))
		{
			context.RemoteOffset = offset;
		}
		else
		{
			context.RemoteOffset = chunk.Offset + chunk.Length;
		}
	}

	public Task CompleteAsync(UploadSessionContext context, CancellationToken cancellationToken) => Task.CompletedTask;

	public async Task AbortAsync(UploadSessionContext context, CancellationToken cancellationToken)
	{
		if (!options.DeleteRemoteOnCancel)
			return;

		if (!context.ProtocolState.TryGetValue(LocationKey, out var location) || string.IsNullOrWhiteSpace(location))
			return;

		if (context.ProtocolState.TryGetValue(ExtensionsKey, out var extensions)
			&& extensions.Contains("termination", StringComparison.OrdinalIgnoreCase) == false)
		{
			return;
		}

		using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(location, UriKind.Absolute));
		request.Headers.TryAddWithoutValidation("Tus-Resumable", TusResumable);
		HttpRequestFactory.ApplyHeaders(request, context.Headers);

		try
		{
			using var response = await HttpRequestFactory.SendAsync(http, request, cancellationToken).ConfigureAwait(false);
		}
		catch (SmartUploadException)
		{
			// Best-effort remote cleanup.
		}
	}

	internal static string BuildMetadata(UploadSessionContext context)
	{
		var pairs = new List<string>
		{
			Encode("filename", context.FileName)
		};

		if (!string.IsNullOrWhiteSpace(context.ContentType))
			pairs.Add(Encode("contentType", context.ContentType));

		foreach (var item in context.Metadata)
		{
			if (string.Equals(item.Key, "filename", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(item.Key, "contentType", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			pairs.Add(Encode(item.Key, item.Value));
		}

		return string.Join(",", pairs);
	}

	static string Encode(string key, string? value)
	{
		var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
		return $"{key} {Convert.ToBase64String(bytes)}";
	}
}
