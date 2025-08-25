using InventoryApi.Data;
using InventoryApi.Dtos;
using InventoryApi.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public AdminsController(AppDbContext context) => _context = context;

        // GET: /api/Admins/metrics  (SuperAdmin only)
        [HttpGet("metrics")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<IEnumerable<SellerOrderMetricDto>>> GetMetrics()
        {
            // Bills table used as orders; grouped by UserId (seller/admin). Only include bills with at least one item.
            var query = _context.Bills
                .AsNoTracking();

            var grouped = await query
                .GroupBy(b => b.UserId)
                .Select(g => new SellerOrderMetricDto
                {
                    SellerId = g.Key,
                    TotalOrders = g.Count(),
                    PaidOrders = g.Count(x => x.Status == BillStatus.Paid),
                    PendingOrders = g.Count(x => x.Status == BillStatus.Pending),
                    CanceledOrders = g.Count(x => x.Status == BillStatus.Canceled),
                    TotalRevenue = g.Where(x => x.Status == BillStatus.Paid).Sum(x => (decimal?)x.TotalAmount) ?? 0m,
                    PendingRevenue = g.Where(x => x.Status == BillStatus.Pending).Sum(x => (decimal?)x.TotalAmount) ?? 0m,
                    LastPaidAt = g.Where(x => x.Status == BillStatus.Paid).Max(x => (DateTime?)x.PaidAt)
                })
                .ToListAsync();

            return grouped; // 200 [] if empty
        }
    }
}
