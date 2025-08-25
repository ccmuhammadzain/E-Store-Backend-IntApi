using System.Text.Json.Serialization;

namespace InventoryApi.Dtos
{
    public class SellerOrderMetricDto
    {
        [JsonPropertyName("sellerId")] public int SellerId { get; set; }
        [JsonPropertyName("totalOrders")] public int TotalOrders { get; set; }
        [JsonPropertyName("paidOrders")] public int PaidOrders { get; set; }
        [JsonPropertyName("pendingOrders")] public int PendingOrders { get; set; }
        [JsonPropertyName("canceledOrders")] public int CanceledOrders { get; set; }
        [JsonPropertyName("totalRevenue")] public decimal TotalRevenue { get; set; }
        [JsonPropertyName("pendingRevenue")] public decimal PendingRevenue { get; set; }
        [JsonPropertyName("lastPaidAt")] public DateTime? LastPaidAt { get; set; }
    }
}
