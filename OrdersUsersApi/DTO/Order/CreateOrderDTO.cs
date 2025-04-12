namespace OrdersUsersApi.DTO.Order
{
    public class CreateOrderDTO
    {
        public int UserId { get; set; }
        public int ClientId { get; set; }
        public string DeliveryMethod { get; set; } = string.Empty;
        public float DiscountPercent { get; set; }
        public string? DiscountReason { get; set; }
        public decimal CashbackUsed { get; set; }

        public List<OrderProductDTO> Products { get; set; } = new();
    }
}
