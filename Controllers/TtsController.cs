using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using WebReader.Configuration;
using WebReader.Helpers;
using WebReader.Services;
using WebReader.Services.Tts;

namespace WebReader.Controllers;

[Authorize]
[Route("api/[controller]/[action]")]
public class TtsController(
    MinioService minioService,
    HybridCache cache,
    TtsService ttsService,
    IHttpClientFactory httpClientFactory,
    IOptions<TtsConfig> ttsConfig,
    ILogger<TtsController> logger) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    //TODO: remove ttsText. It have to go from s3 text data
    public async Task<IActionResult> GetFile(string ttsId, string ttsLang, string ttsText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ttsId) || string.IsNullOrWhiteSpace(ttsLang) ||
            string.IsNullOrWhiteSpace(ttsText))
            return BadRequest($"Param ({nameof(ttsId)} | {nameof(ttsLang)}| {nameof(ttsText)}) is null or whitespace.");

        var httpClient = httpClientFactory.CreateClient("TTS");

        var customOptions = new HybridCacheEntryOptions
        {
            LocalCacheExpiration = TimeSpan.FromSeconds(3000),
            Expiration = TimeSpan.FromSeconds(3000)
        };

        var presignedUrl = await cache.GetOrCreateWithLoggingAsync(
            $"ttsPresignedUrl_{ttsId}",
            async _ =>
            {
                var objectName = $"{ttsId}.ogg";

                var isExists =
                    await minioService.ObjectExistsAsync(StaticNames.TtsBucketName, objectName, cancellationToken);

                switch (isExists)
                {
                    case false when !ttsConfig.Value.Enabled:
                        return null;
                    case false when ttsConfig.Value.Enabled:
                    {
                        var isGenerated = await ttsService.GenerateSpeechAsync(ttsText, ttsId, cancellationToken);
                        if (!isGenerated) return null;
                        break;
                    }
                }

                return await minioService.GetFileUrlAsync(StaticNames.TtsBucketName, objectName);
            },
            logger,
            cancellationToken: cancellationToken,
            options: customOptions
        );

        if (string.IsNullOrEmpty(presignedUrl))
            return StatusCode(500, "Failed to generate or retrieve file URL.");

        var response =
            await httpClient.GetAsync(presignedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            return StatusCode((int)response.StatusCode, "Failed to fetch file from storage.");
        }

        var contentType = response.Content.Headers.ContentType?.ToString() ?? "audio/ogg";

        if (response.Content.Headers.ContentLength.HasValue)
            Response.ContentLength = response.Content.Headers.ContentLength.Value;

        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
        {
            FileName = $"{ttsId}.ogg"
        }.ToString();

        return File(
            responseStream,
            contentType,
            null,
            true
        );
    }
}
