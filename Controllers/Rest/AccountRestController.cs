using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebReader.Models.Dtos.Rest;
using WebReader.Services;

namespace WebReader.Controllers.Rest;

[ApiController]
[Produces("application/json")]
[Route("api/[controller]/[action]")]
public class AccountRestController(AuthRestService authRestService) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> SignIn(LoginRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var res = await authRestService.SignIn(request);

        if (res.IsFailed) return Unauthorized(new { message = res.ToString() });

        return Ok(res.Value);
    }
}
