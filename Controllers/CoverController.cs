using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Controllers;

[Route("api/[controller]/[action]")]
public class CoverController(
    MinioService minioService,
    FileRepository fileRepository,
    IHttpClientFactory httpClientFactory,
    HybridCache cache,
    ILogger<CoverController> logger) : Controller
{
    [HttpGet]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetCover(Guid bucketId, Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.FirstOrDefaultAsync(f =>
            f.BucketId == bucketId &&
            f.Id == fileId &&
            f.AccessRoles.Intersect(User.GetUserRoles()).Any(), null, cancellationToken, true);

        if (file == null) return new NotFoundResult();

        var httpClient = httpClientFactory.CreateClient("S3");

        var customOptions = new HybridCacheEntryOptions
        {
            LocalCacheExpiration = TimeSpan.FromSeconds(3000),
            Expiration = TimeSpan.FromSeconds(3000)
        };

        var presignedUrl = await cache.GetOrCreateWithLoggingAsync(
            $"filePresignedUrl_covers_{file.CoverName ?? CoverHelper.GetCoverNameByFileType(file.Type)}",
            async _ => await minioService.GetCoverFileUrlAsync(file.CoverName ??
                                                               CoverHelper.GetCoverNameByFileType(file.Type)),
            logger,
            cancellationToken: cancellationToken,
            options: customOptions
        );

        var response =
            await httpClient.GetAsync(presignedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Failed to fetch file from storage.");

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";

        Response.Headers.CacheControl = "public, max-age=86400";

        return File(stream, contentType);
    }
}
