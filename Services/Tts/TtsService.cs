using WebReader.Helpers;

namespace WebReader.Services.Tts;

public class TtsService(
    TtsClientFactory ttsClientFactory,
    ClearTextTtsService clearTextTtsService,
    MinioService minioService,
    ILogger<TtsService> logger)
{
    public async Task<bool> GenerateSpeechAsync(string text, string fileName,
        CancellationToken cancellationToken)
    {
        var cleanedText = await clearTextTtsService.ClearTextAsync(text, cancellationToken);

        var texts = TtsTextHelper.SplitLongText(cleanedText);
        if (texts.Count == 0) return false;

        var tempStreams = new List<MemoryStream>();

        try
        {
            foreach (var t in texts)
            {
                var tempStream = new MemoryStream();
                var success = await DownloadChunkAsync(t, tempStream, cancellationToken);
                if (!success) return false;
                tempStreams.Add(tempStream);
            }

            using var wavStream = new MemoryStream();

            StaticFunctions.MergeWavStreams(tempStreams, wavStream);

            using var oggStream = new MemoryStream();

            AudioConverter.ConvertWavToOpus(wavStream, oggStream);

            return await minioService.UploadTtsAsync(oggStream, fileName + ".ogg", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error converting text to speech");
            return false;
        }
    }

    private async Task<bool> DownloadChunkAsync(string text, MemoryStream targetStream,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await ttsClientFactory.GetClient(TtsProvider.Xtts).ProceedAsync(text, cancellationToken);
            if (!response.IsSuccessStatusCode) return false;

            await using var audioStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await audioStream.CopyToAsync(targetStream, cancellationToken);

            targetStream.Position = 0;

            return true;
        }
        catch
        {
            return false;
        }
    }
}
