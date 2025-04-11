namespace OrdersUsersApi.DTO.Products
{
    public class ProductDto
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public float Weight { get; set; }
        public int? category { get; set; }
    }
}
