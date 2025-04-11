namespace OrdersUsersApi.Models
{
    public class OrderProduct
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int Quantity { get; set; }
        // Это свойство будет вычисляться автоматически
        public decimal UnitPrice => Product?.Price ?? 0;
        public decimal Total => UnitPrice * Quantity;
    }
}
