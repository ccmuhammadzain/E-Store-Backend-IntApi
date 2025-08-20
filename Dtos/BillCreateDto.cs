namespace InventoryApi.Dtos
{
    public class BillCreateDto
    {
        public int UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public List<BillItemCreateDto> BillItems { get; set; }
    }
}