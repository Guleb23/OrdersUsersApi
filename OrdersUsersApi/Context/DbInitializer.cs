using OrdersUsersApi.Migrations;
using OrdersUsersApi.Models;

namespace OrdersUsersApi.Context
{
    public static class DbInitializer
    {
        public static void Seed(AppDbContext context)
        {
            if (!context.Users.Any())
            {
                var user = new User
                {
                    FirstName = "Админ",
                    LastName = "Системный",
                    Email = "admin@example.com",
                    CashbackPercent = 5,
                    Password = BCrypt.Net.BCrypt.HashPassword("Admin123")
                };
                context.Users.Add(user);
                context.SaveChanges();
            }

            if (context.Clients.Any()) return;

            var rnd = new Random();

            // Категории
            var categories = new[]
            {
                new ProductCategory { CategoryName = "Фрукты" },
                new ProductCategory { CategoryName = "Овощи" },
                new ProductCategory { CategoryName = "Молочные продукты" }
            };
            context.ProductCategories.AddRange(categories);

            // Продукты
            var products = new List<Product>
            {
                new() { Name = "Яблоко", Category = categories[0], Price = 60m, Cost = 30m, Weight = 1000f },
                new() { Name = "Банан", Category = categories[0], Price = 80m, Cost = 45m, Weight =1000f },
                new() { Name = "Апельсин", Category = categories[0], Price = 90m, Cost = 50m, Weight = 1000f },
                new() { Name = "Груша", Category = categories[0], Price = 70m, Cost = 35m, Weight = 1000f },
                new() { Name = "Морковь", Category = categories[1], Price = 40m, Cost = 20m, Weight = 1000f },
                new() { Name = "Огурец", Category = categories[1], Price = 50m, Cost = 25m, Weight = 1000f },
                new() { Name = "Помидор", Category = categories[1], Price = 55m, Cost = 30m, Weight = 1000f },
                new() { Name = "Капуста", Category = categories[1], Price = 35m, Cost = 15m, Weight =1000f },
                new() { Name = "Молоко", Category = categories[2], Price = 100m, Cost = 65m, Weight = 1000f },
                new() { Name = "Сметана", Category = categories[2], Price = 120m, Cost = 70m, Weight = 1000f },
                new() { Name = "Йогурт", Category = categories[2], Price = 85m, Cost = 45m, Weight = 1000f },
                new() { Name = "Кефир", Category = categories[2], Price = 90m, Cost = 50m, Weight = 1000f },
                new() { Name = "Брокколи", Category = categories[1], Price = 75m, Cost = 40m, Weight =1000f },
                new() { Name = "Малина", Category = categories[0], Price = 200m, Cost = 110m, Weight = 1000f },
                new() { Name = "Творог", Category = categories[2], Price = 110m, Cost = 60m, Weight = 1000f },
            };
            context.Products.AddRange(products);

            // Клиенты (все принадлежат пользователю с Id = 1)
            var clients = Enumerable.Range(1, 15).Select(i =>
                new Client
                {
                    FullName = $"Клиент {i} Иванов",
                    Phone = $"+79001234{100 + i}",
                    Address = $"г. Город, ул. Улица {i}, д. {i}",
                    Cashback = 0,
                    Comment = i % 3 == 0 ? "Постоянный клиент" : null,
                    UserId = 1
                }
            ).ToList();
            context.Clients.AddRange(clients);

            var now = DateTimeOffset.UtcNow;
            var orders = new List<Order>();

            for (int i = 0; i < 30; i++)
            {
                var isRecent = i >= 15;
                var client = clients[rnd.Next(clients.Count)];
                var date = now.AddDays(isRecent ? -rnd.Next(0, 7) : -rnd.Next(0, 180)).AddHours(rnd.Next(0, 24));
                var productCount = rnd.Next(1, 4);
                var orderProducts = new List<OrderProduct>();

                for (int j = 0; j < productCount; j++)
                {
                    var product = products[rnd.Next(products.Count)];
                    var quantity = rnd.Next(1, 4);
                    orderProducts.Add(new OrderProduct
                    {
                        Product = product,
                        Quantity = quantity
                    });
                }

                var discountPercent = rnd.Next(0, 21);
                var subtotal = orderProducts.Sum(p => p.Total);
                var discount = subtotal * ((decimal)discountPercent / 100);

                var maxUsableCashback = client.Cashback;
                var cashbackUsed = rnd.Next(0, (int)maxUsableCashback + 1);

                var totalAfterDiscount = subtotal - discount;
                var totalAfterCashback = totalAfterDiscount - cashbackUsed;
                if (totalAfterCashback < 0) totalAfterCashback = 0;

                var cashbackEarned = totalAfterDiscount * 0.05m;
                client.Cashback = client.Cashback - cashbackUsed + cashbackEarned;

                var order = new Order
                {
                    Client = client,
                    Date = date,
                    DeliveryMethod = i % 2 == 0 ? "Доставка" : "Самовывоз",
                    Status = isRecent || i % 2 == 0,
                    Products = orderProducts,
                    DiscountPercent = discountPercent,
                    DiscountReason = discountPercent > 0
                        ? (isRecent ? "Скидка недели" : "Скидка по акции")
                        : null,
                    CashbackUsed = cashbackUsed,
                    CashbackEarned = cashbackEarned,
                    TotalPrice = totalAfterCashback
                };

                orders.Add(order);
            }

            context.Orders.AddRange(orders);
            context.SaveChanges();
        }
    }
 }
