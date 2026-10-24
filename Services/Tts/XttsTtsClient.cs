namespace WebReader.Services.Tts;

public class XttsTtsClient(HttpClient httpClient) : ITtsClient
{
    public async Task<HttpResponseMessage> ProceedAsync(string text, string speaker,
        CancellationToken cancellationToken)
    {
        return await httpClient.PostAsJsonAsync("/tts_to_audio/", new
        {
            text,
            speaker_wav = GetVoiceBySpeaker(speaker),
            language = "ru"
        }, cancellationToken);
    }

    private string GetVoiceBySpeaker(string speaker)
    {
        switch (speaker)
        {
            case "female-storier":
                return "olya_new";
            case "female":
                return "dasha_new";
            case "male":
                return "goblin_new";
            case "male-storier":
            default:
                return "vsevolod_new";
        }
    }
}
