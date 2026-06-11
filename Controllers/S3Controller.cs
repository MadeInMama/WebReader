using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using WebReader.Helpers;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Controllers;

[Authorize]
[Route("api/[controller]/[action]")]
public class S3Controller(
    MinioService minioService,
    FileRepository fileRepository,
    IHttpClientFactory httpClientFactory,
    HybridCache cache,
    ILogger<S3Controller> logger) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetFile(Guid bucketId, Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.FirstOrDefaultAsync(f =>
            f.BucketId == bucketId &&
            f.Id == fileId &&
            f.AccessRoles.Intersect(User.GetUserRoles()).Any(), null, cancellationToken, true, f => f.Bucket);

        if (file == null) return new NotFoundResult();

        var httpClient = httpClientFactory.CreateClient("S3");

        var customOptions = new HybridCacheEntryOptions
        {
            LocalCacheExpiration = TimeSpan.FromSeconds(3000),
            Expiration = TimeSpan.FromSeconds(3000)
        };

        var presignedUrl = await cache.GetOrCreateWithLoggingAsync(
            $"filePresignedUrl_{file.Id}",
            async _ => await minioService.GetFileUrlAsync(file.Bucket!.Name, file.Name),
            logger,
            cancellationToken: cancellationToken,
            options: customOptions
        );

        using var response =
            await httpClient.GetAsync(presignedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Failed to fetch file from storage.");

        var contentHeaders = response.Content.Headers;
        Response.ContentType = contentHeaders.ContentType?.ToString();
        Response.ContentLength = contentHeaders.ContentLength;

        await response.Content.CopyToAsync(Response.Body, cancellationToken);

        return new EmptyResult();
    }

    [HttpGet]
    public async Task<IActionResult> GetFileRest(Guid bucketId, Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.FirstOrDefaultAsync(f =>
            f.BucketId == bucketId &&
            f.Id == fileId &&
            f.AccessRoles.Intersect(User.GetUserRoles()).Any(), null, cancellationToken, true, f => f.Bucket);

        if (file == null) return new NotFoundResult();

        var httpClient = httpClientFactory.CreateClient("S3");

        var customOptions = new HybridCacheEntryOptions
        {
            LocalCacheExpiration = TimeSpan.FromSeconds(3000),
            Expiration = TimeSpan.FromSeconds(3000)
        };

        var presignedUrl = await cache.GetOrCreateWithLoggingAsync(
            $"filePresignedUrl_{file.Id}",
            async _ => await minioService.GetFileUrlAsync(file.Bucket!.Name, file.Name),
            logger,
            cancellationToken: cancellationToken,
            options: customOptions
        );

        using var response =
            await httpClient.GetAsync(presignedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Failed to fetch file from storage.");

        var contentHeaders = response.Content.Headers;
        Response.ContentType = contentHeaders.ContentType?.ToString();
        Response.ContentLength = contentHeaders.ContentLength;

        await response.Content.CopyToAsync(Response.Body, cancellationToken);

        return new EmptyResult();
    }
}
