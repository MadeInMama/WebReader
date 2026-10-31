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
            await subscriberPwaRepository.AddAsync(new SubscriberPwa
            {
                Endpoint = subscription.Endpoint,
                Keys = subscription.Keys,
                UserId = User.GetUserGuid()
            }, cancellationToken);
        else
            existingSubscriber.Keys = subscription.Keys;

        await subscriberPwaRepository.SaveChangesAsync(cancellationToken);

        return Accepted();
    }
}
