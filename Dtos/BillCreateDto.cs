using System.Text.Json.Serialization;

namespace InventoryApi.Dtos
{
    public class BillCreateDto
    {
        public List<BillItemCreateDto> BillItems { get; set; } = new();
        [JsonPropertyName("cartItems")] public List<BillItemCreateDto>? CartItems { get; set; }
    }
}