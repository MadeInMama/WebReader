using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebReader.Helpers;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Controllers;

[Authorize]
[Route("api/[controller]/[action]")]
public class S3Controller(
    MinioService minioService,
    FileRepository fileRepository,
    IHttpClientFactory httpClientFactory) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetFile(Guid bucketId, Guid fileId)
    {
        var file = await fileRepository.FirstOrDefaultAsync(f =>
            f.BucketId == bucketId &&
            f.Id == fileId &&
            f.AccessRoles.Intersect(User.GetUserRoles()).Any(), null, true, f => f.Bucket);

        if (file == null) return new NotFoundResult();

        var httpClient = httpClientFactory.CreateClient("S3");

        var presignedUrl = await minioService.GetFileUrlAsync(file.Bucket!.Name, file.Name);

        using var response = await httpClient.GetAsync(presignedUrl, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Failed to fetch file from storage.");

        var contentHeaders = response.Content.Headers;
        Response.ContentType = contentHeaders.ContentType?.ToString();
        Response.ContentLength = contentHeaders.ContentLength;

        await response.Content.CopyToAsync(Response.Body);

        return new EmptyResult();
    }
}
