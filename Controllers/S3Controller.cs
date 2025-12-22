using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebReader.Services;

namespace WebReader.Controllers;

[Authorize]
[Route("api/[controller]/[action]")]
public class S3Controller(MinioService minioService, IHttpClientFactory httpClientFactory) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetAllBuckets(string bucket, string objectName)
    {
        var httpClient = httpClientFactory.CreateClient("S3");

        var presignedUrl = await minioService.GetFileUrlAsync(bucket, objectName);

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
