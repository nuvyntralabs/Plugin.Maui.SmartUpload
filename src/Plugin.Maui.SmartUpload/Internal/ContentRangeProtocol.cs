using System.Net.Http.Headers;

namespace Plugin.Maui.SmartUpload;

sealed class ContentRangeProtocol(HttpClient http) : IUploadProtocol
{
	public const string UploadIdHeader = "X-Upload-Id";
	public const string ChunkIndexHeader = "X-Chunk-Index";
	public const string ChunkCountHeader = "X-Chunk-Count";

	public UploadProtocolKind Kind => UploadProtocolKind.ContentRange;

	public Task InitializeAsync(UploadSessionContext context, CancellationToken cancellationToken)
	{
		context.RemoteUploadId ??= context.SessionId;
		return Task.CompletedTask;
	}

	public async Task QueryRemoteProgressAsync(UploadSessionContext context, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Head, context.Endpoint);
		request.Headers.TryAddWithoutValidation(UploadIdHeader, context.SessionId);
		HttpRequestFactory.ApplyHeaders(request, context.Headers);

		HttpResponseMessage response;
		try
		{
			response = await HttpRequestFactory.SendAsync(http, request, cancellationToken).ConfigureAwait(false);
		}
		catch (SmartUploadException)
		{
			return;
		}

		using (response)
		{
			if ((int)response.StatusCode is < 200 or > 299)
				return;

			if (TryReadAcknowledgedBytes(response, context.FileSize, out var acknowledged))
				context.RemoteOffset = Math.Max(context.RemoteOffset, acknowledged);
		}
	}

	public async Task UploadChunkAsync(UploadSessionContext context, ChunkInfo chunk, Stream chunkStream, CancellationToken cancellationToken)
	{
		using var content = new StreamContent(chunkStream, 64 * 1024);
		content.Headers.ContentType = new MediaTypeHeaderValue(context.ContentType ?? "application/octet-stream");
		content.Headers.ContentLength = chunk.Length;
		content.Headers.ContentRange = new ContentRangeHeaderValue(chunk.Offset, chunk.EndInclusive, context.FileSize);

		using var request = new HttpRequestMessage(context.Method, context.Endpoint)
		{
			Content = content
		};
		request.Headers.TryAddWithoutValidation(UploadIdHeader, context.RemoteUploadId ?? context.SessionId);
		request.Headers.TryAddWithoutValidation(ChunkIndexHeader, chunk.Index.ToString());
		request.Headers.TryAddWithoutValidation(ChunkCountHeader, chunk.TotalChunks.ToString());
		HttpRequestFactory.ApplyHeaders(request, context.Headers);

		using var response = await HttpRequestFactory.SendAsync(http, request, cancellationToken).ConfigureAwait(false);
		HttpRequestFactory.EnsureSuccess(response, extraSuccessCodes: [308]);

		if (TryReadAcknowledgedBytes(response, context.FileSize, out var acknowledged))
			context.RemoteOffset = Math.Max(acknowledged, chunk.Offset + chunk.Length);
		else
			context.RemoteOffset = chunk.Offset + chunk.Length;
	}

	public Task CompleteAsync(UploadSessionContext context, CancellationToken cancellationToken) => Task.CompletedTask;

	public Task AbortAsync(UploadSessionContext context, CancellationToken cancellationToken) => Task.CompletedTask;

	internal static bool TryReadAcknowledgedBytes(HttpResponseMessage response, long fileSize, out long acknowledged)
	{
		acknowledged = 0;

		if (response.Content.Headers.ContentRange is { To: long to })
		{
			acknowledged = to + 1;
			return true;
		}

		if (response.Headers.TryGetValues("Range", out var values))
		{
			var raw = values.FirstOrDefault();
			if (TryParseRangeEnd(raw, out var end))
			{
				acknowledged = end + 1;
				return true;
			}
		}

		if (response.Headers.TryGetValues("X-Last-Byte", out var lastByteValues)
			&& long.TryParse(lastByteValues.FirstOrDefault(), out var lastByte))
		{
			acknowledged = lastByte + 1;
			return true;
		}

		_ = fileSize;
		return false;
	}

	internal static bool TryParseRangeEnd(string? raw, out long end)
	{
		end = 0;
		if (string.IsNullOrWhiteSpace(raw))
			return false;

		// bytes=0-123 or 0-123
		var span = raw.AsSpan().Trim();
		if (span.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
			span = span[6..];

		var dash = span.IndexOf('-');
		if (dash < 0)
			return long.TryParse(span, out end);

		return long.TryParse(span[(dash + 1)..], out end);
	}
}
