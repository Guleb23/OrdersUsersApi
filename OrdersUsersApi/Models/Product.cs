namespace OrdersUsersApi.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public ProductCategory? Category { get; set; }
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public float Weight { get; set; }
    }
}
