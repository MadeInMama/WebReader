namespace WebReader.Services.Tts;

public interface ITtsClient
{
    Task<HttpResponseMessage> ProceedAsync(string text, CancellationToken cancellationToken);
}
