namespace InventoryApi.Dtos
{
    public class BillPaymentDto
    {
        public string CustomerName { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? PaymentReference { get; set; }
    }
}
