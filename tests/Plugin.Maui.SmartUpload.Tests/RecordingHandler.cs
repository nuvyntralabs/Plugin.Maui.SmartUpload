using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Plugin.Maui.SmartUpload.Tests;

sealed class RecordingHandler : HttpMessageHandler
{
	readonly Func<HttpRequestMessage, byte[], HttpResponseMessage> _responder;

	public RecordingHandler(Func<HttpRequestMessage, byte[], HttpResponseMessage> responder)
	{
		_responder = responder;
	}

	public ConcurrentBag<RecordedRequest> Requests { get; } = [];

	public TimeSpan Delay { get; set; }

	public int FailNext { get; set; }

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		if (Delay > TimeSpan.Zero)
			await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);

		var body = request.Content is null
			? []
			: await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

		Requests.Add(new RecordedRequest(
			request.Method,
			request.RequestUri,
			request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase),
			request.Content?.Headers.ContentRange?.ToString(),
			body));

		if (FailNext > 0 && request.Method != HttpMethod.Head)
		{
			FailNext--;
			return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
			{
				Content = new StringContent("try again", Encoding.UTF8, "text/plain")
			};
		}

		return _responder(request, body);
	}

	public sealed record RecordedRequest(
		HttpMethod Method,
		Uri? Uri,
		Dictionary<string, string> Headers,
		string? ContentRange,
		byte[] Body);
}
