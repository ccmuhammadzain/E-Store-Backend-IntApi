using System.Text.Json.Serialization;

namespace InventoryApi.Dtos
{
    public class AdminUserDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("createdAt")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("isActive")] public bool IsActive { get; set; } = true;
    }
}
