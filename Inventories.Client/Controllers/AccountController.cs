using Microsoft.AspNetCore.Mvc;

namespace Inventories.Client.Controllers;

public class AccountController : Controller
{
    // Cookie authentication redirects here when an authenticated user
    // fails an authorization check (e.g. [Authorize(Roles = "admin")]).
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }
}
