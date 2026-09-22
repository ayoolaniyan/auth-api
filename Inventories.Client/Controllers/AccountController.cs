using Microsoft.AspNetCore.Mvc;

namespace Inventories.Client.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
