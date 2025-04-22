using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrdersUsersApi.Context;
using OrdersUsersApi.DTO.Catrgory;
using OrdersUsersApi.DTO.Client;
using OrdersUsersApi.DTO.Order;
using OrdersUsersApi.DTO.Products;
using OrdersUsersApi.Models;
using System.Runtime.Intrinsics.Arm;
using System.Xml.Linq;

namespace OrdersUsersApi.DashboardMain
{
    public static class DashboardEndpoints
    {
       
        public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/dashboard").WithTags("Dashboard");

            // 1. Мини-статистика
            group.MapGet("/mini-stats", async (AppDbContext db, int userId) =>
            {
                var now = DateTimeOffset.UtcNow;
                var start = new DateTimeOffset(new DateTime(now.Year, now.Month, 1), TimeSpan.Zero);

                var orders = await db.Orders
                    .Where(o => o.Date >= start && o.Date <= now && o.Client.UserId == userId)
                    .Include(o => o.Products)
                    .ToListAsync();

                var totalUnitsSold = orders.Sum(o => o.Products.Sum(p => p.Quantity));
                var totalBuyers = orders.Select(o => o.ClientId).Distinct().Count();
                var totalOrders = orders.Count;
                var totalRevenue = orders.Sum(o => o.TotalPrice);

                return Results.Ok(new
                {
                    totalUnitsSold,
                    totalBuyers,
                    totalOrders,
                    totalRevenue
                });
            });

            // 2. График выручки
            group.MapGet("/revenue-chart", async (AppDbContext db, int userId) =>
            {
                // Текущая дата в UTC с правильным Kind
                var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

                // --- Текущая неделя ---
                var weekStart = today.AddDays(-6); // Начало недели 7 дней назад
                var weekEnd = today.AddDays(1); // Завтра (чтобы включить сегодня полностью)

                var weekOrders = await db.Orders
                    .Where(o => o.Date.UtcDateTime >= weekStart && o.Date.UtcDateTime < weekEnd && o.Client.UserId == userId)
                    .Include(o => o.Products)
                    .ToListAsync();

                // --- Полгода: с 1-го числа 6 месяцев назад по конец текущего месяца ---
                var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var sixMonthsAgoStart = new DateTime(monthStart.AddMonths(-5).Year, monthStart.AddMonths(-5).Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var currentMonthEnd = new DateTime(monthStart.Year, monthStart.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddMonths(1)
                    .AddDays(-1);

                var halfYearOrders = await db.Orders
                    .Where(o => o.Date.UtcDateTime >= sixMonthsAgoStart && o.Date.UtcDateTime <= currentMonthEnd && o.Client.UserId == userId)
                    .Include(o => o.Products)
                    .ToListAsync();

                // --- Группировка по дням (неделя) ---
                var allWeekDays = Enumerable.Range(0, (int)(today - weekStart).TotalDays + 1)
                    .Select(d => weekStart.AddDays(d))
                    .ToList();

                var weekData = allWeekDays
                    .Select(day =>
                    {
                        var dayOrders = weekOrders.Where(o => o.Date.Date == day.Date).ToList();
                        return new
                        {
                            Label = day.ToString("dd.MM"),
                            RevenueK = dayOrders.Sum(x => x.TotalPrice) / 1000,
                            Units = dayOrders.Sum(x => x.Products.Sum(p => p.Quantity))
                        };
                    })
                    .OrderBy(g => g.Label)
                    .ToList();

                // --- Группировка по месяцам (полгода) ---
                var halfYearData = halfYearOrders
                    .GroupBy(o => new { o.Date.Year, o.Date.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => new
                    {
                        Label = $"{g.Key.Month:D2}.{g.Key.Year}",
                        RevenueK = g.Sum(x => x.TotalPrice) / 1000,
                        Units = g.Sum(x => x.Products.Sum(p => p.Quantity))
                    })
                    .ToList();

                // --- Финальный результат ---
                var result = new
                {
                    halfYear = new
                    {
                        revenue = halfYearOrders.Sum(x => x.TotalPrice) / 1000,
                        categories = halfYearData.Select(d => d.Label).ToArray(),
                        lineChartData = new object[]
                        {
                new { name = "Выручка, тыс.₽", data = halfYearData.Select(d => d.RevenueK).ToArray() },
                new { name = "Продажи", data = halfYearData.Select(d => d.Units).ToArray() }
                        }
                    },
                    week = new
                    {
                        revenue = weekOrders.Sum(x => x.TotalPrice) / 1000,
                        categories = weekData.Select(d => d.Label).ToArray(),
                        lineChartData = new object[]
                        {
                new { name = "Выручка, тыс.₽", data = weekData.Select(d => d.RevenueK).ToArray() },
                new { name = "Продажи", data = weekData.Select(d => d.Units).ToArray() }
                        }
                    }
                };

                return Results.Ok(result);
            });




            // 3. Популярные категории
            group.MapGet("/popular-categories", async (AppDbContext db, int userId) =>
            {
                var now = DateTimeOffset.UtcNow;
                var start = now.AddMonths(-1);

                var categoryCounts = await db.OrderProducts
                    .Where(op => op.Order.Date >= start && op.Order.Date <= now && op.Order.Client.UserId == userId)
                    .Include(op => op.Product).ThenInclude(p => p.Category)
                    .GroupBy(op => op.Product.Category.CategoryName)
                    .Select(g => new
                    {
                        Category = g.Key,
                        Count = g.Sum(x => x.Quantity)
                    })
                    .ToListAsync();

                var total = categoryCounts.Sum(c => c.Count);
                var top3 = categoryCounts.OrderByDescending(c => c.Count).Take(3).ToList();
                var otherCount = total - top3.Sum(x => x.Count);

                var result = top3.Select(c => new
                {
                    Category = c.Category,
                    Percentage = Math.Round((double)c.Count / total * 100, 2),
                    Count = c.Count
                }).ToList();

                if (otherCount > 0)
                {
                    result.Add(new
                    {
                        Category = "Другое",
                        Percentage = Math.Round((double)otherCount / total * 100, 2),
                        Count = otherCount
                    });
                }

                return Results.Ok(result);
            });

            // 4. Последние продажи
            group.MapGet("/recent-sales", async (AppDbContext db, int userId) =>
            {
                var recentOrders = await db.Orders
                    .Where(o => o.Client.UserId == userId)
                    .Include(o => o.Client)
                    .OrderByDescending(o => o.Date)
                    .Take(10)
                    .Select(o => new
                    {
                        ID = o.Id.ToString(),
                        client = o.Client.FullName,
                        cost = $"{o.TotalPrice:F0}₽",
                        date = o.Date.ToString("dd.MM.yyyy")
                    })
                    .ToListAsync();

                return Results.Ok(recentOrders);
            });
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

            // 5. Топ-10 продуктов
            group.MapGet("/top10-products", async (AppDbContext db, int userId) =>
            {
                var topProducts = await db.OrderProducts
                    .Where(op => op.Order.Client.UserId == userId)
                    .GroupBy(op => new { op.ProductId, op.Product.Name })
                    .OrderByDescending(g => g.Sum(x => x.Quantity))
                    .Take(10)
                    .Select(g => new
                    {
                        g.Key.ProductId,
                        g.Key.Name,
                        Quantity = g.Sum(x => x.Quantity)
                    })
                    .ToListAsync();

                return Results.Ok(topProducts);
            });

            // 5. Топ-10 клиентов
            group.MapGet("/top10-clients", async (AppDbContext db, int userId) =>
            {
                var topClients = await db.Orders
                    .Where(o => o.Client.UserId == userId)
                    .GroupBy(o => new { o.ClientId, o.Client.FullName })
                    .OrderByDescending(g => g.Sum(o => o.TotalPrice))
                    .Take(10)
                    .Select(g => new
                    {
                        g.Key.ClientId,
                        g.Key.FullName,
                        Total = g.Sum(o => o.TotalPrice)
                    })
                    .ToListAsync();

                return Results.Ok(topClients);
            });

            // 6. Все продукты — без фильтра, так как продукты общие
            group.MapGet("/all-products", async (AppDbContext db) =>
            {
                try
                {
                    var products = await db.Products
                        .Include(p => p.Category)
                        .Select(p => new
                        {
                            id = p.Id,
                            name = p.Name,
                            category = p.Category.CategoryName,
                            weight = p.Weight,
                            price = p.Price,
                        })
                        .ToListAsync();

                    return Results.Ok(products);
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Ошибка при получении продуктов: {ex.Message}");
                }
            });


            group.MapGet("/all-clients/{userId}", async (AppDbContext db, int userId) =>
            {
                try
                {
                    var clients = await db.Clients
                        .Where(c => c.UserId == userId)
                        .Select(c => new
                        {
                            id = c.Id,
                            fullName = c.FullName,
                            phone = c.Phone,
                            address = c.Address,
                            cashback = c.Cashback,
                            comment = c.Comment
                        })
                        .ToListAsync();

                    return Results.Ok(clients);
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Ошибка при получении клиентов: {ex.Message}");
                }
            });
            // Удаление продукта
            group.MapDelete("/product/{id}", async (AppDbContext db, int id) =>
            {
                var product = await db.Products.FindAsync(id);

                if (product == null)
                {
                    return Results.NotFound($"Продукт с ID {id} не найден.");
                }

                try
                {
                    db.Products.Remove(product);
                    await db.SaveChangesAsync();
                    return Results.Ok($"Продукт с ID {id} был успешно удален.");
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Ошибка при удалении продукта: {ex.Message}");
                }
            });
            // Редактирование продукта
            group.MapPut("/product/{id}", async (AppDbContext db, int id, ProductDto productDto) =>
            {
                var product = await db.Products.FindAsync(id);

                if (product == null)
                {
                    return Results.NotFound($"Продукт с ID {id} не найден.");
                }

                try
                {
                    // Обновляем данные продукта
                    product.Name = productDto.Name;
                    product.Price = productDto.Price;
                    product.Weight = productDto.Weight;

                    // Можно обновить и категорию, если она передана
                    if (productDto.category.HasValue)
                    {
                        var category = await db.ProductCategories.FindAsync(productDto.category);
                        if (category != null)
                        {
                            product.CategoryId = category.Id;
                        }
                    }

                    await db.SaveChangesAsync();
                    return Results.Ok($"Продукт с ID {id} был успешно обновлен.");
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Ошибка при обновлении продукта: {ex.Message}");
                }
            });

            group.MapGet("/categories", async (AppDbContext db) =>
            {
                return db.ProductCategories.ToList();
            });

            group.MapPost("/createCategory", async (AppDbContext db, CategoryDTO dto) =>
            {
                var exstinCat = db.ProductCategories.FirstOrDefault(c => c.CategoryName == dto.name);
                if(exstinCat == null)
                {
                    ProductCategory newCat = new ProductCategory()
                    {
                        CategoryName = dto.name
                    };
                    db.ProductCategories.Add(newCat);
                    await db.SaveChangesAsync();
                    return Results.Ok(newCat);
                }
                else
                {
                    return Results.Conflict();
                }
            });

            // Создание нового продукта
            group.MapPost("/product", async (AppDbContext db, ProductDto productDto) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(productDto.Name) || productDto.Price <= 0 || productDto.Weight <= 0)
                    {
                        return Results.BadRequest("Пожалуйста, укажите корректные данные для продукта.");
                    }

                    var category = await db.ProductCategories.FindAsync(productDto.category);

                    if (category == null)
                    {
                        return Results.BadRequest("Категория не найдена.");
                    }

                    var newProduct = new Product
                    {
                        Name = productDto.Name,
                        Price = productDto.Price,
                        Weight = productDto.Weight,
                        CategoryId = category.Id
                    };

                    db.Products.Add(newProduct);
                    await db.SaveChangesAsync();

                    return Results.Ok(new
                    {
                        message = "Продукт успешно создан",
                        product = new
                        {
                            id = newProduct.Id,
                            name = newProduct.Name,
                            category = category.CategoryName,
                            weight = newProduct.Weight,
                            price = newProduct.Price
                        }
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Ошибка при создании продукта: {ex.Message}");
                }
            });
            // Удаление клиента
            group.MapDelete("/client/{userId}/{id}", async (AppDbContext db, int userId, int id) =>
            {
                var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

                if (client == null)
                    return Results.NotFound($"Клиент с ID {id} не найден.");

                try
                {
                    db.Clients.Remove(client);
                    await db.SaveChangesAsync();
                    return Results.Ok($"Клиент с ID {id} удален.");
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Ошибка при удалении клиента: {ex.Message}");
                }
            });
            // Обновление данных клиента
            group.MapPut("/client/{userId}/{id}", async (AppDbContext db, int userId, int id, ClientDTO clientDto) =>
            {
                var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

                if (client == null)
                    return Results.NotFound($"Клиент с ID {id} не найден.");

                try
                {
                    client.FullName = clientDto.FullName;
                    client.Phone = clientDto.Phone;
                    client.Address = clientDto.Address;
                    client.Cashback = clientDto.Cashback;

                    await db.SaveChangesAsync();
                    return Results.Ok($"Клиент с ID {id} успешно обновлен.");
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Ошибка при обновлении клиента: {ex.Message}");
                }
            });

            // Создание нового клиента
            group.MapPost("/createClient", async (AppDbContext db, NewClientDTO clientDto) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(clientDto.FullName) || string.IsNullOrEmpty(clientDto.Phone))
                        return Results.BadRequest("Укажите корректные данные.");

                    var newClient = new Client
                    {
                        FullName = clientDto.FullName,
                        Phone = clientDto.Phone,
                        Address = clientDto.Address,
                        Cashback = 0,
                        Comment = clientDto.Comment,
                        UserId = clientDto.UserId
                    };

                    db.Clients.Add(newClient);
                    await db.SaveChangesAsync();

                    return Results.Ok(new
                    {
                        message = "Клиент успешно создан",
                        client = new
                        {
                            id = newClient.Id,
                            fullName = newClient.FullName,
                            phone = newClient.Phone,
                            address = newClient.Address,
                            cashback = newClient.Cashback
                        }
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Ошибка при создании клиента: {ex.Message}");
                }
            });
            group.MapGet("/client-details/{name}", async (AppDbContext db, string name) =>
            {
                var client = await db.Clients
                    .Include(c => c.Orders)
                        .ThenInclude(o => o.Products)
                            .ThenInclude(op => op.Product)
                                .ThenInclude(p => p.Category)
                    .FirstOrDefaultAsync(c => c.FullName == name);

                if (client == null)
                {
                    return Results.NotFound($"Клиент с именем {name} не найден.");
                }

                var result = new
                {
                    id = client.Id,
                    fullName = client.FullName,
                    phone = client.Phone,
                    address = client.Address,
                    cashback = client.Cashback,
                    orders = client.Orders.Select(o => new
                    {
                        orderId = o.Id,
                        date = o.Date.ToString("dd.MM.yyyy"),
                        totalPriceWithoutDiscount = o.Products.Sum(p => p.Total),
                        discountPercent = o.DiscountPercent,
                        discountAmount = o.Products.Sum(p => p.Total) * ((decimal)o.DiscountPercent / 100),
                        cashbackUsed = o.CashbackUsed,
                        cashbackEarned = o.CashbackEarned, // добавлено сюда!
                        finalTotalPrice = o.TotalPrice,
                        products = o.Products.Select(op => new
                        {
                            name = op.Product.Name,
                            category = op.Product.Category.CategoryName,
                            quantity = op.Quantity,
                            price = op.Product.Price,
                            total = op.Total
                        })
                    }).OrderByDescending(o => o.date)
                };

                return Results.Ok(result);
            });
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

        }
    }

}
