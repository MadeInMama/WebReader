using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebReader.Helpers;
using WebReader.Repositories;

namespace WebReader.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class PwaNotificationController(
    SubscriberPwaRepository subscriberPwaRepository,
    ILogger<PwaNotificationController> logger) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Subscribe(
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Subscribe Data");

        if (!User.Identity?.IsAuthenticated == true)
            return Accepted();

        var userId = User.GetUserGuid();


        logger.LogInformation("Subscribe Data {}", userId);

        return Accepted();
    }
}
