namespace Plugin.Maui.SmartUpload;

static class ContentTypeResolver
{
	public static string Resolve(string fileName, string? explicitType)
	{
		if (!string.IsNullOrWhiteSpace(explicitType))
			return explicitType;

		return Path.GetExtension(fileName).ToLowerInvariant() switch
		{
			".jpg" or ".jpeg" => "image/jpeg",
			".png" => "image/png",
			".gif" => "image/gif",
			".webp" => "image/webp",
			".pdf" => "application/pdf",
			".mp4" => "video/mp4",
			".mov" => "video/quicktime",
			".mp3" => "audio/mpeg",
			".zip" => "application/zip",
			".json" => "application/json",
			".txt" => "text/plain",
			".csv" => "text/csv",
			".xml" => "application/xml",
			_ => "application/octet-stream"
		};
	}
}
