using System.Text.Json.Serialization;

namespace InventoryApi.models
{
    public class Bill
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }

        public List<BillItem> BillItems { get; set; }

        [JsonIgnore]  
        public User User { get; set; }
    }
}
