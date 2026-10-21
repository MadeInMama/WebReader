using WebReader.Helpers;
using WebReader.Services.Tts;

namespace WebReader.Services;

public class TtsService(TtsClientFactory ttsClientFactory, MinioService minioService, ILogger<TtsService> logger)
{
    public async Task<bool> GenerateSpeechAsync(string text, string fileName,
        CancellationToken cancellationToken)
    {
        var cleanedText = TtsTextHelper.PrepareTextForTts(text);

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

            // var tmpFolderPath = Path.Combine(Path.GetTempPath(), $"tts_{Guid.NewGuid()}");
            // Directory.CreateDirectory(tmpFolderPath);
            //
            // var fileWavPath = Path.Combine(tmpFolderPath, fileName + ".wav");
            // await using var fileWavStream = File.OpenWrite(fileWavPath);
            // wavStream.Position = 0;
            // await wavStream.CopyToAsync(fileWavStream, cancellationToken);
            //
            // var fileOggPath = Path.Combine(tmpFolderPath, fileName + ".ogg");
            // await using var fileOggStream = File.OpenWrite(fileOggPath);
            // oggStream.Position = 0;
            // await oggStream.CopyToAsync(fileOggStream, cancellationToken);
            //
            // return true;

            return await minioService.UploadTtsAsync(oggStream, fileName + ".ogg", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error converting text to speech");
            return false;
        }
        // finally
        // {
        //     if (Directory.Exists(tempFolder))
        //         Directory.Delete(tempFolder, true);
        // }
    }

    private async Task<bool> DownloadChunkAsync(string text, MemoryStream targetStream,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await ttsClientFactory.GetClient(TtsProvider.Xtts).SynthesizeAsync(text, cancellationToken);
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
