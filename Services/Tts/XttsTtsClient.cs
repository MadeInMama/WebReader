namespace WebReader.Services.Tts;

public class XttsTtsClient(HttpClient httpClient) : ITtsClient
{
    public async Task<HttpResponseMessage> ProceedAsync(string text, CancellationToken cancellationToken)
    {
        return await httpClient.PostAsJsonAsync("/tts_to_audio/", new
        {
            text,
            speaker_wav = "dasha1",
            language = "ru"
        }, cancellationToken);
    }
}
