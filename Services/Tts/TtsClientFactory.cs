namespace WebReader.Services.Tts;

public class TtsClientFactory(IServiceProvider serviceProvider)
{
    public ITtsClient GetClient(TtsProvider provider)
    {
        return provider switch
        {
            TtsProvider.Xtts => serviceProvider.GetRequiredService<XttsTtsClient>(),
            _ => throw new ArgumentException("Unknown TTS Provider", nameof(provider))
        };
    }
}
