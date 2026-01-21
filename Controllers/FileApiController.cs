using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebReader.Models.Dtos;
using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]/[action]")]
public class FileApiController(UserReadingRepository readingRepository) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReading([FromBody] UpdateReadingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var reading = await readingRepository.FirstOrDefaultAsync(f =>
            f.UserId == request.UserId && f.FileId == request.FileId, null);

        if (reading == null)
        {
            await readingRepository.AddAsync(new UserReading
            {
                FileId = request.FileId, UserId = request.UserId, Page = request.Page, Scale = request.Scale,
                IsDone = request.IsLastPage
            });
            await readingRepository.SaveChangesAsync();
        }
        else
        {
            await readingRepository.SetCurrPageAndScaleAndIsDoneAsync(reading.Id, request.Page, request.Scale,
                request.IsLastPage);
        }

        return Accepted();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReading([FromBody] DeleteReadingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await readingRepository.DeleteAllAsync([request.ReadingId]);

        return NoContent();
    }
}
