using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plugin.Maui.SmartUpload;

static class SessionSerializer
{
	internal static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
	};

	public static string Serialize(UploadSessionRecord record) =>
		JsonSerializer.Serialize(record, Options);

	public static UploadSessionRecord? Deserialize(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return null;

		return JsonSerializer.Deserialize<UploadSessionRecord>(json, Options);
	}
}
