namespace InventoryApi.Dtos
{
    public record ProductCreateDto(string Title, string Category, string? Brand, decimal Price, int Stock, string? ProductImage);
    public record ProductDto(int Id, string Title, string Category, string? Brand, decimal Price, int Stock, string? ProductImage, int OwnerId, string OwnerUsername);
}
