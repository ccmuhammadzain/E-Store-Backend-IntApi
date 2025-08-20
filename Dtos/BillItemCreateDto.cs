namespace InventoryApi.Dtos
{
    public class BillItemCreateDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}