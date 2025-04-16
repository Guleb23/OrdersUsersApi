namespace OrdersUsersApi.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public int CashbackPercent { get; set; } = 5;

        // Навигационное свойство
        public ICollection<Client> Clients { get; set; } = new List<Client>();
    }
}


