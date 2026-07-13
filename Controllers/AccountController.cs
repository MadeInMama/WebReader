using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebReader.Helpers;
using WebReader.Models.Dtos;
using WebReader.Models.Entities;
using WebReader.Services;

namespace WebReader.Controllers;

[Route("[controller]/[action]")]
public class AccountController(UserService userService) : Controller
{
    [HttpGet]
    [ServiceFilter(typeof(LogRequestAttribute))]
    public IActionResult SignIn(string? returnUrl = null)
    {
        if (User.Identity is not { IsAuthenticated: true })
            return View(new LoginViewModel { ReturnUrl = returnUrl });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [ServiceFilter(typeof(LogRequestAttribute))]
    public IActionResult SignUp(string? returnUrl = null)
    {
        if (User.Identity is not { IsAuthenticated: true })
            return View(new RegisterViewModel { ReturnUrl = returnUrl });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [EnableRateLimiting("LoginPolicy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(LoginViewModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            ClearPasswordFromResponse(model);
            return View(model);
        }

        var user = await userService.AuthenticateAsync(model.Username, model.Password, cancellationToken);

        if (user != null) return await SetUser(user, model.RememberMe, model.ReturnUrl);

        ModelState.AddModelError(string.Empty, "Invalid username or password.");

        ClearPasswordFromResponse(model);

        return View(model);
    }

    [HttpPost]
    [EnableRateLimiting("LoginPolicy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignUp(RegisterViewModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            ClearPasswordFromResponse(model);
            return View(model);
        }

        var user = await userService.CreateUserAsync(model.Username, model.Password, cancellationToken);

        if (user != null)
        {
            return await SetUser(user, model.RememberMe, model.ReturnUrl);
        }

        ModelState.AddModelError(string.Empty, "Username is already taken or password does not meet requirements.");

        ClearPasswordFromResponse(model);

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity is { IsAuthenticated: false })
            return LocalRedirect(Url.Action("Index", "Home")!);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    public IActionResult CustomNotFound()
    {
        return View();
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken = default)
    {
        var id = User.GetUserGuid();
        await userService.DeleteUserAsync(id, cancellationToken);
        return RedirectToAction("Logout", "Account");
    }

    private async Task<IActionResult> SetUser(CustomUser user, bool rememberMe, string? returnUrl = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username)
        };
        claims.AddRange(user.Roles.Select(f => new Claim(ClaimTypes.Role, f.ToString())));

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            claimsPrincipal,
            authProperties);

        return LocalRedirect(returnUrl ?? Url.Action("Index", "Home")!);
    }

    private void ClearPasswordFromResponse(LoginViewModel model)
    {
        ModelState.Remove(nameof(model.Password));
        model.Password = string.Empty;
    }

    private void ClearPasswordFromResponse(RegisterViewModel model)
    {
        ModelState.Remove(nameof(model.Password));
        model.Password = string.Empty;
    }
}
