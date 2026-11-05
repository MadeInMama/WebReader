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
    public async Task<IActionResult> Subscribe([FromBody] SubscribeDto? subscription,
        CancellationToken cancellationToken = default)
    {
        if (subscription == null ||
            string.IsNullOrWhiteSpace(subscription.Endpoint) ||
            subscription.Keys.Count == 0 ||
            User.Identity?.IsAuthenticated == false) return NoContent();

        var existingSubscriber = await subscriberPwaRepository.FirstOrDefaultAsync(
            f => f.Endpoint == subscription.Endpoint, null, cancellationToken);

        if (existingSubscriber == null)
        {
            await subscriberPwaRepository.AddAsync(new SubscriberPwa
            {
                Endpoint = subscription.Endpoint,
                Keys = subscription.Keys,
                UserId = User.GetUserGuid(),
                DeviceName = subscription.DeviceName ?? "Unknown device"
            }, cancellationToken);
        }
        else
        {
            existingSubscriber.Keys = subscription.Keys;
            existingSubscriber.DeviceName = subscription.DeviceName ?? "Unknown device";
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
}
