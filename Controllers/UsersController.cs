using InventoryApi.Data;
using InventoryApi.Dtos;
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
                    Email = null, // field not in model; placeholder for future
                    CreatedAt = u.CreatedAt,
                    IsActive = true // no flag in model; assume active
                })
                .ToListAsync();
            return admins; // 200 [] if empty
        }
    }
}
