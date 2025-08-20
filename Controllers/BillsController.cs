using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryApi.Data;
using InventoryApi.models;
using InventoryApi.Dtos;

namespace InventoryApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BillsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BillsController(AppDbContext context)
        {
            _context = context;
        }

        // ✅ GET: api/Bills
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Bill>>> GetBills()
        {
            return await _context.Bills
                .Include(b => b.BillItems)
                .ThenInclude(bi => bi.Product)
                .Include(b => b.User) // include user details too
                .ToListAsync();
        }

        // ✅ GET: api/Bills/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Bill>> GetBill(int id)
        {
            var bill = await _context.Bills
                .Include(b => b.BillItems)
                .ThenInclude(bi => bi.Product)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bill == null)
            {
                return NotFound();
            }

            return bill;
        }

        // ✅ POST: api/Bills
        [HttpPost]
        public async Task<ActionResult<Bill>> PostBill(BillCreateDto billDto)
        {
            // Map DTO → Entity
            var bill = new Bill
            {
                UserId = billDto.UserId,
           
                Date = DateTime.Now,
                BillItems = billDto.BillItems.Select(item => new BillItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Price
                }).ToList()
            };
            bill.TotalAmount = bill.BillItems.Sum(item => item.Price * item.Quantity);

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBill), new { id = bill.Id }, bill);
        }
    }
}
