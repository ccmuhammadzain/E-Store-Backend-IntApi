namespace InventoryApi.Dtos
{
    public record LoginDto(string Username, string Password);
    public record SignupDto(string Username, string Password, string? Role);
}
