using OrdersUsersApi.Models;

public class Order
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }
    public DateTimeOffset Date { get; set; }
    public string DeliveryMethod { get; set; }
    public bool Status { get; set; }

    public ICollection<OrderProduct> Products { get; set; } = new List<OrderProduct>();

    // Убираем set, делаем вычисляемым
    public decimal TotalPrice { get; set; }

    public float DiscountPercent { get; set; }
    public string? DiscountReason { get; set; }
    public decimal CashbackEarned { get; set; } 
    public decimal CashbackUsed { get; set; }

    public decimal CalculateTotalPrice()
    {
        var subtotal = Products.Sum(p => p.Total);
        var discount = subtotal * ((decimal)DiscountPercent / 100);
        var total = subtotal - discount - CashbackUsed;
        return total >= 0 ? total : 0; // На случай, если скидка/кешбэк больше суммы
    }
}

