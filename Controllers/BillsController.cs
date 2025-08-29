using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryApi.Data;
using InventoryApi.models;
using InventoryApi.Dtos;
using System.Security.Claims;
using InventoryApi.Extensions;

namespace InventoryApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BillsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public BillsController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: api/Bills
        // Rules:
        // - Customer: only their own bills
        // - Admin/Seller: bills that include at least one product they own
        // - SuperAdmin: all bills
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Bill>>> GetBills()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            IQueryable<Bill> query = _context.Bills
                .Include(b => b.BillItems).ThenInclude(bi => bi.Product)
                .Include(b => b.User);

            if (role == "SuperAdmin")
            {
                return await query.ToListAsync();
            }
            else if (role == "Admin" || role == "Seller")
            {
                // Bills that have at least one item whose product owner is this admin/seller
                query = query.Where(b => b.BillItems.Any(it => it.Product.OwnerId == userId));
                return await query.ToListAsync();
            }
            else
            {
                // Customer: only own bills
                query = query.Where(b => b.UserId == userId);
                return await query.ToListAsync();
            }
        }

        // GET: api/Bills/5 (same visibility rules)
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<Bill>> GetBill(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            var bill = await _context.Bills
                .Include(b => b.BillItems).ThenInclude(bi => bi.Product)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (bill == null) return NotFound();

            bool authorized = role switch
            {
                "SuperAdmin" => true,
                "Admin" or "Seller" => bill.BillItems.Any(it => it.Product.OwnerId == userId),
                _ => bill.UserId == userId
            };
            if (!authorized) return Forbid();
            return bill;
        }

        // POST: api/Bills (create pending bill from cart) - customer / any authenticated user placing order
        [HttpPost]
        [Authorize]
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
        [Authorize]
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
        [Authorize]
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

        private static List<BillItemCreateDto> NormalizeItems(BillCreateDto dto)
        {
            if (dto.BillItems != null && dto.BillItems.Count > 0)
                return dto.BillItems;
            if (dto.CartItems != null && dto.CartItems.Count > 0)
                return dto.CartItems;
            return new List<BillItemCreateDto>();
        }
    }
}
