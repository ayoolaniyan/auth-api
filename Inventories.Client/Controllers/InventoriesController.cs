using Microsoft.AspNetCore.Mvc;
using Inventories.Client.ApiServices;
using Inventories.Client.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Inventories.Client.Controllers
{
    [Authorize]
    public class InventoriesController : Controller
    {
        private IInventoryApiService _inventoryApiService;

        public InventoriesController(IInventoryApiService inventoryApiService)
        {
            _inventoryApiService = inventoryApiService ?? throw new ArgumentNullException(nameof(inventoryApiService));
        }

        // GET: Inventories
        public async Task<IActionResult> Index()
        {
            await LogTokenAndClaims();
            return View(await _inventoryApiService.GetInventories());
        }
        public async Task LogTokenAndClaims()
        {
            var identityToken = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.IdToken);

            Debug.WriteLine($"Identity token: {identityToken}");

            foreach (var claim in User.Claims)
            {
                Debug.WriteLine($"Claim type: {claim.Type} - Claim value: {claim.Value}");
            }
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> OnlyAdmin()
        {
            var userInfo = await _inventoryApiService.GetUserInfo();
            return View(userInfo);            
        }

        // GET: Inventories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var inventory = await _inventoryApiService.GetInventory(id.ToString());
            if (inventory == null)
            {
                return NotFound();
            }

            return View(inventory);
        }

        // GET: Inventories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Inventories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Genre,Rating,ReleaseDate,ImageUrl,Owner")] Inventory inventory)
        {
            if (!ModelState.IsValid)
            {
                return View(inventory);
            }

            await _inventoryApiService.CreateInventory(inventory);
            return RedirectToAction(nameof(Index));
        }

        // GET: Inventories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var inventory = await _inventoryApiService.GetInventory(id.ToString());
            if (inventory == null)
            {
                return NotFound();
            }

            return View(inventory);
        }

        // POST: Inventories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Genre,Rating,ReleaseDate,ImageUrl,Owner")] Inventory inventory)
        {
            if (id != inventory.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(inventory);
            }

            await _inventoryApiService.UpdateInventory(inventory);
            return RedirectToAction(nameof(Index));
        }

        // GET: Inventories/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var inventory = await _inventoryApiService.GetInventory(id.ToString());
            if (inventory == null)
            {
                return NotFound();
            }

            return View(inventory);
        }

        // POST: Inventories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _inventoryApiService.DeleteInventory(id);
            return RedirectToAction(nameof(Index));
        }

        public async Task Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}
