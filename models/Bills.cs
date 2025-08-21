using System.Text.Json.Serialization;

namespace InventoryApi.models
{
    public enum BillStatus
    {
        Pending = 0,
        Paid = 1,
        Canceled = 2
    }

    public class Bill
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }

        public BillStatus Status { get; set; } = BillStatus.Pending;
        public DateTime? PaidAt { get; set; }
        public string? PaymentReference { get; set; }

        // Basic checkout / shipping details captured at payment time
        public string? CustomerName { get; set; }
        public string? AddressLine1 { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Phone { get; set; }

        public List<BillItem> BillItems { get; set; } = new();

        [JsonIgnore]
        public User User { get; set; }
    }
}
