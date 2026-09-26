using Inventories.API.Caching;
using Inventories.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inventories.API.Models;

namespace Inventories.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize("ClientIdPolicy")]
    public class InventoriesController : ControllerBase
    {
        private readonly InventoriesContext _context;
        private readonly InventoryCache _cache;

        public InventoriesController(InventoriesContext context, InventoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // GET: api/Inventories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Inventory>>> GetInventory()
        {
            var inventories = await _cache.GetAllAsync(async () => await _context.Inventories.ToListAsync(), HttpContext.RequestAborted);
            return inventories!;
        }

        // GET: api/Inventory/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Inventory>> GetInventory(int id)
        {
            var inventory = await _cache.GetAsync(id, async () => await _context.Inventories.FindAsync(id), HttpContext.RequestAborted);

            if (inventory == null)
            {
                return NotFound();
            }

            return inventory;
        }

        // PUT: api/Inventory/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutInventory(int id, Inventory inventory)
        {
            if (id != inventory.Id)
            {
                return BadRequest();
            }

            _context.Entry(inventory).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _cache.InvalidateAsync(id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InventoryExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Inventory
        [HttpPost]
        public async Task<ActionResult<Inventory>> PostInventory(Inventory inventory)
        {
            _context.Inventories.Add(inventory);
            await _context.SaveChangesAsync();
            await _cache.InvalidateAsync();

            return CreatedAtAction("GetInventory", new { id = inventory.Id }, inventory);
        }

        // DELETE: api/Inventory/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<Inventory>> DeleteInventory(int id)
        {
            var inventory = await _context.Inventories.FindAsync(id);
            if (inventory == null)
            {
                return NotFound();
            }

            _context.Inventories.Remove(inventory);
            await _context.SaveChangesAsync();
            await _cache.InvalidateAsync(id);

            return inventory;
        }

        private bool InventoryExists(int id)
        {
            return _context.Inventories.Any(e => e.Id == id);
        }
    }
}
