namespace InventoryApi.models
{
    public class Product
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Category { get; set; } = null!;
        public string? Brand { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string? ProductImage { get; set; }
        public int OwnerId { get; set; }
        public User Owner { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}