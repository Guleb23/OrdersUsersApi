using Microsoft.EntityFrameworkCore;
using OrdersUsersApi.Context;
using OrdersUsersApi.DTO.Order;
using OrdersUsersApi.DTO.Products;
using OrdersUsersApi.Models;

public static class OrdersEndpoints
{
    public static RouteGroupBuilder MapOrdersEndpoints(this RouteGroupBuilder group)
    {
        // Все заказы
        group.MapGet("/all-sales", async (AppDbContext db, int userId) =>
        {
            var allOrders = await db.Orders
                .Where(o => o.Client.UserId == userId)
                .Include(o => o.Client)
                .OrderByDescending(o => o.Date)
                .Select(o => new
                {
                    ID = o.Id.ToString(),
                    client = o.Client.FullName,
                    cost = $"{o.TotalPrice:F0}₽",
                    date = o.Date.ToString("dd.MM.yyyy"),
                    status = o.Status
                })
                .ToListAsync();

            return Results.Ok(allOrders);
        });

        // Подробная информация о заказе
        group.MapGet("/orderdetails/{userId:int}/{orderId:int}", async (AppDbContext db, int userId, int orderId) =>
        {
            var order = await db.Orders
                .Include(o => o.Client)
                .Include(o => o.Products)
                    .ThenInclude(op => op.Product)
                        .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.Client.UserId == userId);

            if (order == null)
                return Results.NotFound($"Заказ с ID {orderId} для пользователя {userId} не найден.");

            var result = new
            {
                orderId = order.Id,
                date = order.Date.ToString("dd.MM.yyyy"),
                deliveryMethod = order.DeliveryMethod,
                status = order.Status,
                client = new
                {
                    id = order.Client.Id,
                    fullName = order.Client.FullName,
                    phone = order.Client.Phone,
                    address = order.Client.Address
                },
                discountPercent = order.DiscountPercent,
                discountReason = order.DiscountReason,
                cashbackUsed = order.CashbackUsed,
                cashbackEarned = order.CashbackEarned,
                finalTotalPrice = order.TotalPrice,
                products = order.Products.Select(op => new
                {
                    name = op.Product.Name,
                    category = op.Product.Category.CategoryName,
                    quantity = op.Quantity,
                    price = op.Product.Price,
                    total = op.Total
                }),
                totalPriceWithoutDiscount = order.Products.Sum(p => p.Total),
                discountAmount = order.Products.Sum(p => p.Total) * ((decimal)order.DiscountPercent / 100)
            };

            return Results.Ok(result);
        });

        //Создание заказа
        group.MapPost("/createOrder", async (AppDbContext db, CreateOrderDTO dto) =>
        {
            var client = await db.Clients
                .FirstOrDefaultAsync(c => c.Id == dto.ClientId && c.UserId == dto.UserId);
            if (client == null)
                return Results.BadRequest("Клиент не найден.");

            if (client.Cashback < dto.CashbackUsed)
                return Results.BadRequest("Недостаточно кешбэка.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == dto.UserId);
            if (user == null)
                return Results.BadRequest("Пользователь не найден.");

            var cashbackPercent = user.CashbackPercent / 100m;

            var order = new Order
            {
                ClientId = dto.ClientId,
                Date = DateTimeOffset.UtcNow,
                DeliveryMethod = dto.DeliveryMethod,
                DiscountPercent = dto.DiscountPercent,
                DiscountReason = dto.DiscountReason,
                CashbackUsed = dto.CashbackUsed,
                Status = false
            };

            foreach (var item in dto.Products)
            {
                var product = await db.Products.FindAsync(item.ProductId);
                if (product == null)
                    return Results.BadRequest($"Продукт с ID {item.ProductId} не найден.");

                order.Products.Add(new OrderProduct
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                });
            }

            var productIds = order.Products.Select(p => p.ProductId).ToList();
            var productsFromDb = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            var subtotal = productsFromDb.Sum(p =>
            {
                var quantity = order.Products.First(op => op.ProductId == p.Id).Quantity;
                return p.Price * quantity;
            });

            var discount = subtotal * ((decimal)order.DiscountPercent / 100m);
            var finalPrice = subtotal - discount - order.CashbackUsed;
            order.TotalPrice = finalPrice >= 0 ? finalPrice : 0;

            // Используем процент кешбэка из пользователя
            order.CashbackEarned = (subtotal - discount) * cashbackPercent;

            client.Cashback = client.Cashback - order.CashbackUsed + order.CashbackEarned;

            db.Orders.Add(order);
            db.Clients.Update(client);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Заказ успешно создан",
                orderId = order.Id,
                finalPrice = order.TotalPrice,
                cashbackEarned = order.CashbackEarned,
                updatedClientCashback = client.Cashback
            });
        });

        //Удаление заказа
        group.MapDelete("/order/{orderId}", async (AppDbContext db, int orderId) =>
        {
            var order = await db.Orders
                .Include(o => o.Products) // Загружаем связанные продукты
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return Results.NotFound($"Заказ с ID {orderId} не найден.");

            try
            {
                // Удалим связанные записи в OrderProduct (если каскад не настроен)
                db.OrderProducts.RemoveRange(order.Products);

                // Удалим сам заказ
                db.Orders.Remove(order);

                await db.SaveChangesAsync();

                return Results.Ok($"Заказ с ID {orderId} успешно удалён.");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Ошибка при удалении заказа: {ex.Message}");
            }
        });

        // Удаление продукта из заказа
        group.MapDelete("/order/{userId}/{orderId}/product/{productName}", async (AppDbContext db, int userId, int orderId, string productName) =>
        {
            var order = await db.Orders
                .Include(o => o.Products)
                    .ThenInclude(op => op.Product)
                .Include(o => o.Client)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.Client.UserId == userId);

            if (order is null)
                return Results.NotFound($"Заказ с ID {orderId} не найден.");

            var orderProduct = order.Products
                .FirstOrDefault(op => op.Product.Name.Equals(productName, StringComparison.OrdinalIgnoreCase));

            if (orderProduct is null)
                return Results.NotFound($"Продукт '{productName}' не найден в заказе.");

            order.Products.Remove(orderProduct);
            order.TotalPrice = order.CalculateTotalPrice();

            try
            {
                await db.SaveChangesAsync();
                return Results.Ok($"Продукт '{productName}' удалён. Общая сумма пересчитана.");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Ошибка при удалении: {ex.Message}");
            }
        });

        // Обновление количества продукта в заказе
        group.MapPut("/order/{orderId}/product/{productName}", async (AppDbContext db, int orderId, string productName, OrderProductDTO dto) =>
        {
            var order = await db.Orders
                .Include(o => o.Products)
                    .ThenInclude(op => op.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return Results.NotFound($"Заказ с ID {orderId} не найден.");

            // Найдем старый продукт в заказе, который нужно заменить
            var orderProduct = order.Products
                .FirstOrDefault(op => op.Product.Name.Equals(productName, StringComparison.OrdinalIgnoreCase));

            // Если старый продукт найден, удаляем его из заказа
            if (orderProduct != null)
            {
                order.Products.Remove(orderProduct);  // Удаляем старый продукт
            }
            try
            {
                // Ищем новый продукт по имени, которое пришло в запросе
                var newProduct = await db.Products
.Include(p => p.Category)
.FirstOrDefaultAsync(p => p.Name.ToLower() == dto.ProductName.ToLower());
                if (newProduct == null)
                {
                    return Results.NotFound($"Продукт '{dto.ProductName}' не найден.");
                }

                // Создаем новый объект связи между заказом и новым продуктом
                var newOrderProduct = new OrderProduct
                {
                    ProductId = newProduct.Id,
                    Product = newProduct,
                    Quantity = dto.Quantity  // Устанавливаем количество
                };

                // Добавляем новый продукт в заказ
                order.Products.Add(newOrderProduct);
            }
            catch (Exception ex)
            {
                // Логирование ошибки
                Console.WriteLine($"Error occurred: {ex.Message}");
                throw;
            }


            // Пересчитываем итоговую стоимость заказа
            order.TotalPrice = order.CalculateTotalPrice();

            try
            {
                await db.SaveChangesAsync();  // Сохраняем изменения
                return Results.Ok($"Продукт '{productName}' был заменен на '{dto.ProductName}'. Итоговая сумма пересчитана.");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Ошибка при обновлении: {ex.Message}");
            }
        });

        // Добавление продукта в существующий заказ
        group.MapPost("/order/{orderId}/product", async (AppDbContext db, int orderId, AddProductToOrderDTO dto) =>
        {
            // Находим заказ с включенными продуктами и клиентом
            var order = await db.Orders
                .Include(o => o.Products)
                    .ThenInclude(op => op.Product)
                .Include(o => o.Client)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return Results.NotFound($"Заказ с ID {orderId} не найден.");

            // Проверяем, существует ли продукт
            var product = await db.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId);

            if (product == null)
                return Results.NotFound($"Продукт с ID {dto.ProductId} не найден.");

            // Проверяем, не добавлен ли уже этот продукт в заказ
            var existingProduct = order.Products.FirstOrDefault(op => op.ProductId == dto.ProductId);

            if (existingProduct != null)
            {
                // Если продукт уже есть в заказе - увеличиваем количество
                existingProduct.Quantity += dto.Quantity;
            }
            else
            {
                // Если продукта нет в заказе - добавляем новый
                order.Products.Add(new OrderProduct
                {
                    ProductId = dto.ProductId,
                    Quantity = dto.Quantity
                });
            }

            // Пересчитываем стоимость заказа
            var productIds = order.Products.Select(p => p.ProductId).ToList();
            var productsFromDb = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();

            var subtotal = productsFromDb.Sum(p =>
            {
                var quantity = order.Products.First(op => op.ProductId == p.Id).Quantity;
                return p.Price * quantity;
            });

            var discount = subtotal * ((decimal)order.DiscountPercent / 100m);
            var finalPrice = subtotal - discount - order.CashbackUsed;
            order.TotalPrice = finalPrice >= 0 ? finalPrice : 0;

            // Пересчитываем кэшбэк (5% от суммы после скидки)
            order.CashbackEarned = (subtotal - discount) * 0.05m;

            try
            {
                await db.SaveChangesAsync();
                return Results.Ok(new
                {
                    message = $"Продукт '{product.Name}' успешно добавлен в заказ",
                    orderId = order.Id,
                    finalPrice = order.TotalPrice,
                    cashbackEarned = order.CashbackEarned
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Ошибка при добавлении продукта: {ex.Message}");
            }
        });

        // Обновление статуса оплачен/неоплачен
        group.MapPut("/order/{orderId}/status", async (AppDbContext db, int orderId, UpdateOrderStatusDTO dto) =>
        {
            // Находим заказ по ID
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);

            // Если заказ не найден, возвращаем ошибку 404
            if (order == null)
                return Results.NotFound($"Заказ с ID {orderId} не найден.");

            // Обновляем статус заказа
            order.Status = dto.Status;

            try
            {
                // Сохраняем изменения в базе данных
                await db.SaveChangesAsync();

                // Возвращаем успешный ответ с новым статусом
                return Results.Ok(new
                {
                    message = $"Статус заказа с ID {orderId} обновлён на {(dto.Status ? "оплачено" : "не оплачено")}.",
                    newStatus = order.Status
                });
            }
            catch (Exception ex)
            {
                // Обработка ошибки при сохранении
                return Results.Problem($"Ошибка при обновлении статуса: {ex.Message}");
            }
        });
        return group;
    }
}
