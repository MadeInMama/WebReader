using WebReader.Models.Dtos.Tts;

namespace WebReader.Services.Tts;

public class XttsTtsClient(HttpClient httpClient) : ITtsClient
{
    public async Task<HttpResponseMessage> SynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        return await httpClient.PostAsJsonAsync("/tts_to_audio/", new XttsRequest
        {
            Text = text
        }, cancellationToken);
    }
}
