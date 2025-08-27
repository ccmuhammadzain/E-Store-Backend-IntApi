using BCrypt.Net;
using InventoryApi.Data;
using InventoryApi.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace InventoryApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
        { "SuperAdmin", "Admin", "Seller", "Customer" };

        private static string NormalizeRole(string? role)
        {
            if (string.IsNullOrWhiteSpace(role)) return "Customer";
            // Map ignoring case to canonical forms
            if (role.Equals("superadmin", StringComparison.OrdinalIgnoreCase)) return "SuperAdmin";
            if (role.Equals("admin", StringComparison.OrdinalIgnoreCase)) return "Admin";
            if (role.Equals("seller", StringComparison.OrdinalIgnoreCase)) return "Seller";
            if (role.Equals("customer", StringComparison.OrdinalIgnoreCase)) return "Customer";
            return "Customer"; // fallback
        }

        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> Signup(User user)
        {
            user.Role = NormalizeRole(user.Role);
            if (!AllowedRoles.Contains(user.Role)) return BadRequest("Invalid role");
            if (await _context.Users.AnyAsync(u => u.Username == user.Username))
                return BadRequest("Username already exists");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Ok("User created");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(User login)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == login.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(login.PasswordHash, user.PasswordHash))
                return Unauthorized("Invalid credentials");

            // Normalize stored role if legacy data has different casing
            var normalized = NormalizeRole(user.Role);
            if (normalized != user.Role)
            {
                user.Role = normalized;
                await _context.SaveChangesAsync();
            }

            var token = GenerateJwtToken(user);
            return Ok(new { token, role = user.Role, id = user.Id });
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(5),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
