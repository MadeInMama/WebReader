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
    public async Task<IActionResult> UpdatePage([FromBody] UpdatePageRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var reading = await readingRepository.FirstOrDefaultAsync(f =>
            f.UserId == request.UserId && f.FileId == request.FileId);

        if (reading == null)
            reading = await readingRepository.AddAsync(new UserReading
                { FileId = request.FileId, UserId = request.UserId, Page = request.Page, Scale = request.Scale });
        else
            await readingRepository.SetCurrPageAsync(reading.Id, request.Page, request.Scale);

        return Ok(reading);
    }
}
