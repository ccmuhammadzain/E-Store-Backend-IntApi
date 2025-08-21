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
        private readonly IWebHostEnvironment _env;

        public BillsController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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

        // DEBUG: schema + pending migrations (remove in production)
        [HttpGet("debug/schema")]
        [AllowAnonymous]
        public async Task<IActionResult> DebugSchema()
        {
            // List columns in Bills table & pending migrations
            var columns = await _context.Database
                .SqlQueryRaw<string>("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bills'")
                .ToListAsync();
            var pending = await _context.Database.GetPendingMigrationsAsync();
            return Ok(new { columns, pendingMigrations = pending });
        }

        private static List<BillItemCreateDto> NormalizeItems(BillCreateDto dto)
        {
            if (dto.BillItems != null && dto.BillItems.Count > 0)
                return dto.BillItems;
            if (dto.CartItems != null && dto.CartItems.Count > 0)
                return dto.CartItems;
            return new List<BillItemCreateDto>();
        }

        // POST: api/Bills (create pending bill from cart)
        [HttpPost]
        public async Task<ActionResult<Bill>> PostBill(BillCreateDto billDto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { error = "Missing user id claim", code = "MISSING_USER_CLAIM" });

                var incomingItems = NormalizeItems(billDto);
                if (incomingItems.Count == 0)
                    return BadRequest(new { error = "Bill must contain at least one item.", code = "EMPTY_ITEMS" });

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
                        return BadRequest(new { error = $"Product {c.ProductId} not found.", code = "PRODUCT_NOT_FOUND" });
                    if (c.Quantity <= 0)
                        return BadRequest(new { error = $"Quantity for product {c.ProductId} must be > 0.", code = "INVALID_QUANTITY" });
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
                    TotalAmount = billItems.Sum(i => i.Price * i.Quantity),
                    Status = BillStatus.Pending
                };

                _context.Bills.Add(bill);
                await _context.SaveChangesAsync();

                await _context.Entry(bill).Collection(b => b.BillItems).LoadAsync();
                foreach (var item in bill.BillItems)
                {
                    await _context.Entry(item).Reference(i => i.Product).LoadAsync();
                }

                return CreatedAtAction(nameof(GetBill), new { id = bill.Id }, bill);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BillCreateError] {ex}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[BillCreateInner] {ex.InnerException.Message}");
                }
                var dev = _env.IsDevelopment();
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    code = "INTERNAL",
                    detail = dev ? ex.ToString() : null
                });
            }
        }

        // POST: api/Bills/{id}/pay  (mark bill as paid and capture checkout details)
        [HttpPost("{id}/pay")]
        public async Task<IActionResult> PayBill(int id, BillPaymentDto paymentDto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { error = "Missing user id claim", code = "MISSING_USER_CLAIM" });

            var bill = await _context.Bills.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (bill == null) return NotFound(new { error = "Bill not found", code = "NOT_FOUND" });
            if (bill.Status == BillStatus.Paid) return BadRequest(new { error = "Bill already paid", code = "ALREADY_PAID" });
            if (bill.Status == BillStatus.Canceled) return BadRequest(new { error = "Bill canceled", code = "CANCELED" });

            bill.Status = BillStatus.Paid;
            bill.PaidAt = DateTime.UtcNow;
            bill.CustomerName = paymentDto.CustomerName;
            bill.AddressLine1 = paymentDto.AddressLine1;
            bill.City = paymentDto.City;
            bill.Country = paymentDto.Country;
            bill.Phone = paymentDto.Phone;
            bill.PaymentReference = string.IsNullOrWhiteSpace(paymentDto.PaymentReference) ? Guid.NewGuid().ToString("N") : paymentDto.PaymentReference;

            await _context.SaveChangesAsync();

            return Ok(new { bill.Id, bill.Status, bill.PaidAt, bill.PaymentReference });
        }

        // DELETE: api/Bills/{id} (cancel bill if still pending and owned by user)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBill(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { error = "Missing user id claim", code = "MISSING_USER_CLAIM" });

            var bill = await _context.Bills.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (bill == null) return NotFound(new { error = "Bill not found", code = "NOT_FOUND" });
            if (bill.Status == BillStatus.Paid) return BadRequest(new { error = "Cannot delete a paid bill", code = "PAID_IMMUTABLE" });

            bill.Status = BillStatus.Canceled;
            await _context.SaveChangesAsync();
            return Ok(new { bill.Id, bill.Status });
        }
    }
}
