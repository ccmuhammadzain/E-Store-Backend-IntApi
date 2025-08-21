using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryApi.Data;
using InventoryApi.models;
using InventoryApi.Dtos;
using System.Security.Claims;

namespace InventoryApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BillsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BillsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Bills (could restrict to current user later)
        [HttpGet]
        [AllowAnonymous] 
        public async Task<ActionResult<IEnumerable<Bill>>> GetBills()
        {
            return await _context.Bills
                .Include(b => b.BillItems)
                .ThenInclude(bi => bi.Product)
                .Include(b => b.User)
                .ToListAsync();
        }

        // GET: api/Bills/5
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Bill>> GetBill(int id)
        {
            var bill = await _context.Bills.FirstOrDefaultAsync(b => b.Id == id);
            if (bill == null)
            {
                return NotFound();
            }
            await _context.Entry(bill).Collection(b => b.BillItems).LoadAsync();
            await _context.Entry(bill).Reference(b => b.User).LoadAsync();
            foreach (var item in bill.BillItems)
            {
                await _context.Entry(item).Reference(i => i.Product).LoadAsync();
            }
            return bill;
        }

        private static List<BillItemCreateDto> NormalizeItems(BillCreateDto dto)
        {
            if (dto.BillItems != null && dto.BillItems.Count > 0)
                return dto.BillItems;
            if (dto.CartItems != null && dto.CartItems.Count > 0)
                return dto.CartItems;
            return new List<BillItemCreateDto>();
        }

        // POST: api/Bills
        [HttpPost]
        public async Task<ActionResult<Bill>> PostBill(BillCreateDto billDto)
        {
            // Extract user id from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { error = "Missing user id claim", code = "MISSING_USER_CLAIM" });
            }

            var incomingItems = NormalizeItems(billDto);
            if (incomingItems.Count == 0)
            {
                return BadRequest(new { error = "Bill must contain at least one item.", code = "EMPTY_ITEMS" });
            }

            // Group duplicate product lines (if any) and sum quantities
            var consolidated = incomingItems
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .ToList();

            var productIds = consolidated.Select(c => c.ProductId).ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var c in consolidated)
            {
                if (!products.ContainsKey(c.ProductId))
                {
                    return BadRequest(new { error = $"Product {c.ProductId} not found.", code = "PRODUCT_NOT_FOUND" });
                }
                if (c.Quantity <= 0)
                {
                    return BadRequest(new { error = $"Quantity for product {c.ProductId} must be > 0.", code = "INVALID_QUANTITY" });
                }
            }

            var billItems = consolidated.Select(c => new BillItem
            {
                ProductId = c.ProductId,
                Quantity = c.Quantity,
                Price = products[c.ProductId].Price
            }).ToList();

            var bill = new Bill
            {
                UserId = userId,
                Date = DateTime.UtcNow,
                BillItems = billItems,
                TotalAmount = billItems.Sum(i => i.Price * i.Quantity)
            };

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();

            await _context.Entry(bill).Collection(b => b.BillItems).LoadAsync();
            await _context.Entry(bill).Reference(b => b.User).LoadAsync();
            foreach (var item in bill.BillItems)
            {
                await _context.Entry(item).Reference(i => i.Product).LoadAsync();
            }

            return CreatedAtAction(nameof(GetBill), new { id = bill.Id }, bill);
        }
    }
}
