using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InventoryApi.models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string Role { get; set; } = "Customer"; // default role
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        // Deactivation / status
        public bool IsActive { get; set; } = true;
        public DateTime? DeactivatedAt { get; set; }
        public int? DeactivatedById { get; set; }
        [JsonIgnore]
        public User? DeactivatedBy { get; set; }
        [Timestamp]
        public byte[]? RowVersion { get; set; }
        // Admin level (badge) - default 1 for Admin role
        public int Level { get; set; } = 1;
    }
}
