using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebReader.Helpers;
using WebReader.Models.Dtos;
using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class PwaNotificationController(SubscriberPwaRepository subscriberPwaRepository) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeDto? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Endpoint) ||
            request.Keys.Count == 0 ||
            User.Identity?.IsAuthenticated == false) return NoContent();

        var existingSubscriber = await subscriberPwaRepository.FirstOrDefaultAsync(
            f => f.Endpoint == request.Endpoint, null, cancellationToken);

        if (existingSubscriber == null)
        {
            await subscriberPwaRepository.AddAsync(new SubscriberPwa
            {
                Endpoint = request.Endpoint,
                Keys = request.Keys,
                UserId = User.GetUserGuid(),
                DeviceName = request.DeviceName ?? "Unknown device"
            }, cancellationToken);
        }
        else
        {
            existingSubscriber.Keys = request.Keys;
            existingSubscriber.DeviceName = request.DeviceName ?? "Unknown device";
        }

        await subscriberPwaRepository.SaveChangesAsync(cancellationToken);

        return Accepted();
    }

    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Unsubscribe([FromBody] SubscribeDto request,
        CancellationToken cancellationToken = default)
    {
        if (User.Identity?.IsAuthenticated == false) return NoContent();

        var res = await subscriberPwaRepository.DeleteByUserIdAndEndpointAsync(User.GetUserGuid(), request.Endpoint,
            cancellationToken);

        if (res == 0) return NoContent();

        return Accepted();
    }

    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> IsSubscribed([FromBody] SubscribeDto request,
        CancellationToken cancellationToken = default)
    {
        if (User.Identity?.IsAuthenticated == false) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Endpoint)) return BadRequest();

        return Ok(await subscriberPwaRepository.ExistsAsync(User.GetUserGuid(), request.Endpoint, cancellationToken));
    }
}
