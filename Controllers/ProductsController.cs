//ProductsController.cs

using InventoryApi.Data;
using InventoryApi.Dtos;
using InventoryApi.Extensions;
using InventoryApi.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ProductsController(AppDbContext db) => _db = db;

        private static ProductDto ToDto(Product p) => new ProductDto(p.Id, p.Title, p.Category, p.Brand, p.Price, p.Stock, p.ProductImage, p.OwnerId, p.Owner.Username);

        // PUBLIC: list products for customers / shop
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ProductDto>>> ListPublic()
        {
            var list = await _db.Products.Include(p => p.Owner)
                .Where(p => p.IsActive && p.Owner.IsActive)
                .AsNoTracking().ToListAsync();
            return Ok(list.Select(ToDto));
        }

        // PUBLIC: get single product detail
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<ActionResult<ProductDto>> GetPublic(int id)
        {
            var prod = await _db.Products.Include(p => p.Owner)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive && p.Owner.IsActive);
            if (prod == null) return NotFound();
            return Ok(ToDto(prod));
        }

        // POST: create (restricted)
        [HttpPost]
        [Authorize(Roles = "Seller,Admin,SuperAdmin")]
        public async Task<ActionResult<ProductDto>> Create(ProductCreateDto dto)
        {
            try
            {
                const string patch = @"IF OBJECT_ID('dbo.Products','U') IS NOT NULL BEGIN
IF COL_LENGTH('dbo.Products','CreatedAt') IS NULL ALTER TABLE dbo.Products ADD CreatedAt datetime2 NOT NULL CONSTRAINT DF_Products_CreatedAt_Runtime2 DEFAULT (SYSUTCDATETIME());
IF COL_LENGTH('dbo.Products','UpdatedAt') IS NULL ALTER TABLE dbo.Products ADD UpdatedAt datetime2 NULL;
IF COL_LENGTH('dbo.Products','OwnerId') IS NULL ALTER TABLE dbo.Products ADD OwnerId int NULL;
IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND EXISTS (SELECT 1 FROM Users) UPDATE p SET OwnerId = u.Id FROM Products p CROSS APPLY (SELECT TOP 1 Id FROM Users ORDER BY Id) u WHERE p.OwnerId IS NULL;
IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Products_OwnerId' AND object_id=OBJECT_ID('dbo.Products')) CREATE INDEX IX_Products_OwnerId ON dbo.Products(OwnerId);
IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Products_Users_OwnerId') ALTER TABLE dbo.Products ADD CONSTRAINT FK_Products_Users_OwnerId FOREIGN KEY(OwnerId) REFERENCES dbo.Users(Id) ON DELETE RESTRICT;
END";
                await _db.Database.ExecuteSqlRawAsync(patch);
            }
            catch { }

            if (dto.Price < 0 || dto.Stock < 0) return BadRequest("Price/Stock cannot be negative");
            var userId = User.GetUserId();
            // Ensure owner active
            var ownerActive = await _db.Users.Where(u => u.Id == userId).Select(u => u.IsActive).FirstOrDefaultAsync();
            if (!ownerActive) return Forbid();

            var entity = new Product
            {
                Title = dto.Title,
                Category = dto.Category,
                Brand = dto.Brand,
                Price = dto.Price,
                Stock = dto.Stock,
                ProductImage = dto.ProductImage,
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _db.Products.Add(entity);
            await _db.SaveChangesAsync();
            entity = await _db.Products.Include(p => p.Owner).FirstAsync(p => p.Id == entity.Id);
            return CreatedAtAction(nameof(GetPublic), new { id = entity.Id }, ToDto(entity));
        }

        // SELLER / ADMIN inventory (still available)
        [HttpGet("mine")]
        [Authorize(Roles = "Seller,Admin,SuperAdmin")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> Mine()
        {
            var role = User.GetRole();
            if (role == "Seller")
            {
                var userId = User.GetUserId();
                var own = await _db.Products.Include(p => p.Owner)
                    .Where(p => p.OwnerId == userId)
                    .AsNoTracking().ToListAsync();
                return Ok(own.Select(ToDto));
            }
            var list = await _db.Products.Include(p => p.Owner).AsNoTracking().ToListAsync();
            return Ok(list.Select(ToDto));
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Seller,Admin,SuperAdmin")]
        public async Task<IActionResult> Update(int id, ProductCreateDto dto)
        {
            if (dto.Price < 0 || dto.Stock < 0) return BadRequest("Price/Stock cannot be negative");
            var prod = await _db.Products.Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == id);
            if (prod == null) return NotFound();
            if (!prod.IsActive || !prod.Owner.IsActive) return BadRequest("Product or owner inactive");
            if (User.IsInRole("Seller") && prod.OwnerId != User.GetUserId()) return Forbid();
            prod.Title = dto.Title;
            prod.Category = dto.Category;
            prod.Brand = dto.Brand;
            prod.Price = dto.Price;
            prod.Stock = dto.Stock;
            prod.ProductImage = dto.ProductImage;
            prod.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Seller,Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            var prod = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (prod == null) return NotFound();
            if (User.IsInRole("Seller") && prod.OwnerId != User.GetUserId()) return Forbid();
            // soft delete
            prod.IsActive = false;
            prod.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
