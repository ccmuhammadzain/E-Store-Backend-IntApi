using InventoryApi.Data;
using InventoryApi.Dtos;
using InventoryApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        public UsersController(AppDbContext context) => _context = context;

        // GET: /api/Users/admins  (SuperAdmin only)
        [HttpGet("admins")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<IEnumerable<AdminUserDto>>> GetAdmins()
        {
            var admins = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == "Admin" || u.Role == "Seller")
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new AdminUserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Role = u.Role,
                    Email = null,
                    CreatedAt = u.CreatedAt,
                    IsActive = u.IsActive,
                    DeactivatedAt = u.DeactivatedAt,
                    Level = u.Level
                })
                .ToListAsync();
            return admins; // 200 [] if empty
        }

        private async Task<bool> IsLastActiveSuperAdminAsync(int userId)
        {
            var activeSupers = await _context.Users.CountAsync(u => u.Role == "SuperAdmin" && u.IsActive && u.Id != userId);
            // Returns true if AFTER removing userId there would be zero left (meaning current user is the last one)
            return activeSupers == 0; 
        }

        // POST: /api/Users/{id}/deactivate  (SuperAdmin)
        [HttpPost("{id:int}/deactivate")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Deactivate(int id)
        {
            var target = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (target == null) return NotFound();
            if (target.Role == "SuperAdmin")
            {
                // Do not allow removing the last active superadmin
                if (await IsLastActiveSuperAdminAsync(target.Id)) return BadRequest("Cannot deactivate the last active SuperAdmin");
            }
            // Only deactivate Admin / Seller (or SuperAdmin if multiple) per business rule
            if (!(target.Role == "Admin" || target.Role == "Seller" || target.Role == "SuperAdmin")) return BadRequest("Cannot deactivate this role");
            if (!target.IsActive) return NoContent();

            target.IsActive = false;
            target.DeactivatedAt = DateTime.UtcNow;
            target.DeactivatedById = User.GetUserId();

            // Soft hide owned products
            var owned = await _context.Products.Where(p => p.OwnerId == target.Id && p.IsActive).ToListAsync();
            foreach (var p in owned) p.IsActive = false;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("Concurrency conflict while deactivating user");
            }
            return NoContent();
        }

        // POST: /api/Users/{id}/activate  (SuperAdmin)
        [HttpPost("{id:int}/activate")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Activate(int id)
        {
            var target = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (target == null) return NotFound();
            if (!target.IsActive)
            {
                target.IsActive = true;
                target.DeactivatedAt = null;
                target.DeactivatedById = null;
                // Business choice: keep products hidden until explicitly re-enabled? For now re-enable.
                var products = await _context.Products.Where(p => p.OwnerId == target.Id && !p.IsActive).ToListAsync();
                foreach (var p in products) p.IsActive = true;
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    return Conflict("Concurrency conflict while activating user");
                }
            }
            return NoContent();
        }

        // POST: /api/Users/{id}/promote (SuperAdmin) - increases level
        [HttpPost("{id:int}/promote")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Promote(int id)
        {
            var target = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && (u.Role == "Admin" || u.Role == "Seller"));
            if (target == null) return NotFound();
            if (!target.IsActive) return BadRequest("Cannot promote inactive user");
            if (target.Level < int.MaxValue) target.Level++;
            await _context.SaveChangesAsync();
            return Ok(new { target.Id, target.Level });
        }

        // POST: /api/Users/{id}/demote (SuperAdmin) - decreases level but not below 1
        [HttpPost("{id:int}/demote")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Demote(int id)
        {
            var target = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && (u.Role == "Admin" || u.Role == "Seller"));
            if (target == null) return NotFound();
            if (!target.IsActive) return BadRequest("Cannot demote inactive user");
            if (target.Level > 1) target.Level--;
            await _context.SaveChangesAsync();
            return Ok(new { target.Id, target.Level });
        }
    }
}
