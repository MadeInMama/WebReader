using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebReader.Helpers;
using WebReader.Models.Dtos;
using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Controllers;

[Authorize]
[Route("api/[controller]/[action]")]
public class PwaNotificationController(SubscriberPwaRepository subscriberPwaRepository) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeDto? subscription,
        CancellationToken cancellationToken = default)
    {
        if (subscription == null) return BadRequest();

        var existingSubscriber = await subscriberPwaRepository.FirstOrDefaultAsync(
            f => f.Endpoint == subscription.Endpoint, null, cancellationToken);

        if (existingSubscriber == null)
        {
            await subscriberPwaRepository.AddAsync(new SubscriberPwa
            {
                Endpoint = subscription.Endpoint,
                Keys = subscription.Keys,
                UserId = User.GetUserGuid()
            }, cancellationToken);
        }
        else
        {
            existingSubscriber.Keys = subscription.Keys;
            await subscriberPwaRepository.SaveChangesAsync(cancellationToken);
        }

        return Ok();
    }
}
