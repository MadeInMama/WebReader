using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using WebReader.Configuration;
using WebReader.Helpers;
using WebReader.Repositories;
using WebReader.Services;
using WebReader.Services.Tts;

namespace WebReader.Controllers;

[Authorize]
[Route("api/[controller]/[action]")]
public class TtsController(
    MinioService minioService,
    HybridCache cache,
    TtsService ttsService,
    FileRepository fileRepository,
    IHttpClientFactory httpClientFactory,
    IOptions<TtsConfig> ttsConfig,
    ILogger<TtsController> logger) : Controller
{
    [HttpGet]
    [ValidateAntiForgeryToken]
    [ResponseCache(Duration = 500, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> GetFileRest(Guid bucketId, Guid fileId, string ttsId,
        CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.FirstOrDefaultAsync(f =>
            f.BucketId == bucketId &&
            f.Id == fileId &&
            f.AccessRoles.Intersect(User.GetUserRoles()).Any(), null, cancellationToken, true, f => f.Bucket);

        if (file == null) return new NotFoundResult();

        if (string.IsNullOrWhiteSpace(ttsId))
            return BadRequest($"Param {nameof(ttsId)} is null or whitespace.");

        var httpClient = httpClientFactory.CreateClient("S3");

        var customOptions = new HybridCacheEntryOptions
        {
            LocalCacheExpiration = TimeSpan.FromSeconds(500),
            Expiration = TimeSpan.FromSeconds(500)
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
                        var info = await minioService.GetTtsTextByTtsIdAsync(file.Bucket!.Name, file.Name, ttsId);
                        var isGenerated =
                            await ttsService.GenerateSpeechAsync(info.ttsText, info.ttsType, info.ttsId,
                                cancellationToken);
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
