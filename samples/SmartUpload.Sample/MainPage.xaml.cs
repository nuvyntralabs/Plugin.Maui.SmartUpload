using Plugin.Maui.SmartUpload;

namespace SmartUpload.Sample;

public partial class MainPage : ContentPage, ISmartUploadLogger
{
	const string TusEndpoint = "https://tusd.tusdemo.net/files/";
	const string ContentRangeEndpoint = "https://httpbin.org/put";

	readonly ISmartUploadClient _client;
	readonly List<string> _logLines = [];
	string? _filePath;
	string? _latestSessionId;

	public MainPage()
	{
		InitializeComponent();
		_client = Plugin.Maui.SmartUpload.SmartUpload.Current;
		_client.ProgressChanged += OnProgressChanged;
		_client.SessionStateChanged += OnStateChanged;
		_client.SessionCompleted += OnCompleted;
		_client.SessionFailed += OnFailed;
		_client.EnableLogging(true, this);

		EndpointEntry.Text = TusEndpoint;
		_ = RefreshSessionsAsync();
	}

	void OnProtocolToggled(object? sender, ToggledEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(EndpointEntry.Text)
			|| EndpointEntry.Text is TusEndpoint or ContentRangeEndpoint)
		{
			EndpointEntry.Text = e.Value ? TusEndpoint : ContentRangeEndpoint;
		}
	}

	async void OnCreateFileClicked(object? sender, EventArgs e)
	{
		try
		{
			var path = Path.Combine(FileSystem.CacheDirectory, "sample-upload.bin");
			var data = new byte[1024 * 1024];
			Random.Shared.NextBytes(data);
			await File.WriteAllBytesAsync(path, data);
			_filePath = path;
			FileLabel.Text = $"{path}{Environment.NewLine}1.00 MB";
			AppendLog("Created 1 MB sample file.");
		}
		catch (Exception ex)
		{
			StatusLabel.Text = ex.Message;
			AppendLog(ex.ToString());
		}
	}

	async void OnPickFileClicked(object? sender, EventArgs e)
	{
		try
		{
			var result = await FilePicker.Default.PickAsync();
			if (result is null)
				return;

			_filePath = result.FullPath;
			var size = new FileInfo(result.FullPath).Length;
			FileLabel.Text = $"{result.FileName}{Environment.NewLine}{size / 1024d / 1024d:0.00} MB";
			AppendLog($"Picked {result.FileName}.");
		}
		catch (Exception ex)
		{
			StatusLabel.Text = ex.Message;
			AppendLog(ex.ToString());
		}
	}

	async void OnEnqueueClicked(object? sender, EventArgs e)
	{
		if (string.IsNullOrWhiteSpace(_filePath))
		{
			StatusLabel.Text = "Create or pick a file first.";
			return;
		}

		if (!Uri.TryCreate(EndpointEntry.Text, UriKind.Absolute, out var endpoint))
		{
			StatusLabel.Text = "Enter a valid http(s) endpoint.";
			return;
		}

		try
		{
			var session = await _client.EnqueueAsync(new UploadRequest
			{
				FilePath = _filePath,
				Endpoint = endpoint,
				Protocol = TusSwitch.IsToggled ? UploadProtocolKind.Tus : UploadProtocolKind.ContentRange,
				Metadata = new Dictionary<string, string>
				{
					["source"] = "SmartUpload.Sample"
				}
			});

			_latestSessionId = session.SessionId;
			StatusLabel.Text = $"Started {session.SessionId}";
			AppendLog($"Enqueued {session.FileName} as {session.SessionId}.");
			await RefreshSessionsAsync();
		}
		catch (Exception ex)
		{
			StatusLabel.Text = ex.Message;
			AppendLog(ex.ToString());
		}
	}

	async void OnPauseClicked(object? sender, EventArgs e) =>
		await RunLatestAsync("Pausing...", id => _client.PauseAsync(id));

	async void OnResumeClicked(object? sender, EventArgs e) =>
		await RunLatestAsync("Resuming...", id => _client.ResumeAsync(id));

	async void OnRetryClicked(object? sender, EventArgs e) =>
		await RunLatestAsync("Retrying...", id => _client.RetryAsync(id));

	async void OnCancelClicked(object? sender, EventArgs e) =>
		await RunLatestAsync("Cancelling...", id => _client.CancelAsync(id));

	async void OnRemoveClicked(object? sender, EventArgs e) =>
		await RunLatestAsync("Removing...", async id =>
		{
			await _client.RemoveAsync(id);
			if (_latestSessionId == id)
				_latestSessionId = null;
		});

	void OnProgressChanged(object? sender, UploadProgressEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			_latestSessionId = e.Session.SessionId;
			StatusLabel.Text = $"{e.Session.FileName}: {e.Progress.Fraction:P0} ({e.Progress.BytesUploaded}/{e.Progress.TotalBytes})";
		});
		_ = RefreshSessionsAsync();
	}

	void OnStateChanged(object? sender, UploadSessionEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() =>
			AppendLog($"{e.Session.SessionId} -> {e.Session.State}"));
		_ = RefreshSessionsAsync();
	}

	void OnCompleted(object? sender, UploadCompletedEventArgs e) =>
		MainThread.BeginInvokeOnMainThread(() =>
		{
			StatusLabel.Text = $"Completed {e.Session.FileName}";
			AppendLog($"Completed {e.Session.SessionId}");
		});

	void OnFailed(object? sender, UploadFailedEventArgs e) =>
		MainThread.BeginInvokeOnMainThread(() =>
		{
			StatusLabel.Text = e.Message ?? "Upload failed.";
			AppendLog($"Failed {e.Session.SessionId}: {e.Message}");
		});

	async Task RunLatestAsync(string status, Func<string, Task> operation)
	{
		if (string.IsNullOrWhiteSpace(_latestSessionId))
		{
			StatusLabel.Text = "No session yet.";
			return;
		}

		try
		{
			StatusLabel.Text = status;
			await operation(_latestSessionId);
			await RefreshSessionsAsync();
		}
		catch (Exception ex)
		{
			StatusLabel.Text = ex.Message;
			AppendLog(ex.ToString());
		}
	}

	async Task RefreshSessionsAsync()
	{
		try
		{
			var sessions = await _client.GetSessionsAsync();
			if (sessions.Count == 0)
			{
				MainThread.BeginInvokeOnMainThread(() => SessionsLabel.Text = "None.");
				return;
			}

			_latestSessionId ??= sessions[0].SessionId;
			var text = string.Join(Environment.NewLine, sessions.Select(Describe));
			MainThread.BeginInvokeOnMainThread(() => SessionsLabel.Text = text);
		}
		catch (Exception ex)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				SessionsLabel.Text = ex.Message;
				AppendLog(ex.ToString());
			});
		}
	}

	static string Describe(UploadSession session) =>
		$"{session.FileName} — {session.State} {session.Progress.Fraction:P0} ({session.CompletedChunks}/{session.TotalChunks} chunks)";

	public void Log(SmartUploadLogLevel level, string message, Exception? exception = null)
	{
		var line = exception is null
			? $"{DateTime.Now:HH:mm:ss} {level}: {message}"
			: $"{DateTime.Now:HH:mm:ss} {level}: {message} ({exception.GetType().Name})";

		MainThread.BeginInvokeOnMainThread(() => AppendLog(line));
	}

	void AppendLog(string line)
	{
		_logLines.Insert(0, line);
		if (_logLines.Count > 40)
			_logLines.RemoveAt(_logLines.Count - 1);

		LogLabel.Text = string.Join(Environment.NewLine, _logLines);
	}
}
