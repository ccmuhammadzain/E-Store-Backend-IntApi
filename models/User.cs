namespace InventoryApi.models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string Role { get; set; } = "Customer"; // default role
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
