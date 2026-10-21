namespace WebReader.Services.Tts;

public interface ITtsClient
{
    Task<HttpResponseMessage> SynthesizeAsync(string text, CancellationToken cancellationToken);
}
