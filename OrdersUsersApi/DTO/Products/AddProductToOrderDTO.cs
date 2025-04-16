namespace OrdersUsersApi.DTO.Products
{
    public class AddProductToOrderDTO
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
