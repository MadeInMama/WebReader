using System.Text.Json.Serialization;

namespace WebReader.Models.Dtos.Tts;

public class XttsRequest
{
    [JsonPropertyName("text")] public string Text { get; init; }
    [JsonPropertyName("speaker_wav")] public string Speaker { get; init; } = "dasha1";
    [JsonPropertyName("language")] public string Language { get; set; } = "ru";
}
