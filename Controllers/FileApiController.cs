using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Dtos;
using WebReader.Models.Entities;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]/[action]")]
public class FileApiController(
    UserReadingRepository readingRepository,
    FileService fileService,
    HybridCache cache) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ServiceFilter(typeof(LogRequestAttribute))]
    public async Task<IActionResult> UpdateReading([FromBody] UpdateReadingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var reading = await readingRepository.FirstOrDefaultAsync(f =>
            f.UserId == User.GetUserGuid() && f.FileId == request.FileId, null, cancellationToken, true);

        if (reading == null)
        {
            await readingRepository.AddAsync(new UserReading
            {
                FileId = request.FileId, UserId = User.GetUserGuid(), Page = request.Page, Scale = request.Scale,
                IsDone = request.IsLastPage
            }, cancellationToken);
            await readingRepository.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await readingRepository.SetCurrPageAndScaleAndIsDoneAsync(reading.Id, request.Page, request.Scale,
                request.IsLastPage, cancellationToken);
        }

        await cache.RemoveByTagAsync($"files_{User.GetUserGuid()}", cancellationToken);

        return Accepted();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ServiceFilter(typeof(LogRequestAttribute))]
    public async Task<IActionResult> DeleteReading([FromBody] DeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await readingRepository.DeleteAllAsync([request.Id], cancellationToken);

        await cache.RemoveByTagAsync($"files_{User.GetUserGuid()}", cancellationToken);

        return NoContent();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ServiceFilter(typeof(LogRequestAttribute))]
    public async Task<IActionResult> DeleteFile([FromBody] DeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        //TODO: error if file not deleted or not exists
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!User.GetUserRoles().Contains(RoleType.Admin)) return BadRequest(ModelState);

        await fileService.DeleteFileAsync([request.Id], cancellationToken);

        await cache.RemoveByTagAsync($"files_{User.GetUserGuid()}", cancellationToken);

        return NoContent();
    }
}
