namespace OrdersUsersApi.Models
{
    public class Client
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Cashback { get; set; }
        public string? Comment { get; set; }

        // Навигационное свойство
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
