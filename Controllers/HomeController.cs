using Microsoft.AspNetCore.Mvc;

namespace WebReader.Controllers;

[Route("[controller]/[action]")]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
