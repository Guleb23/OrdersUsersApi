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
            group.MapGet("/mini-stats", async (AppDbContext db) =>
            {
                var now = DateTimeOffset.UtcNow;
                var start = new DateTimeOffset(new DateTime(now.Year, now.Month, 1), TimeSpan.Zero);

                // Для отладки: вывести все заказы с датами
                var allOrders = await db.Orders
                    .Select(o => new { o.Id, o.Date })
                    .OrderBy(o => o.Date)
                    .ToListAsync();

                Console.WriteLine("📦 Все заказы в базе:");
                foreach (var o in allOrders)
                {
                    Console.WriteLine($"Заказ {o.Id}: {o.Date} (UTC: {o.Date.UtcDateTime})");
                }
                Console.WriteLine($"Период фильтрации: с {start} по {now}");
                // Основная логика
                var orders = await db.Orders
     .Where(o => o.Date >= start && o.Date <= now)
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

            // 2. График выручки за последние полгода и неделю
            group.MapGet("/revenue-chart", async (AppDbContext db) =>
            {
                var now = DateTimeOffset.UtcNow;

                // Для недели
                var weekStart = now.AddDays(-7); // 7 дней назад

                // Для полугода
                var halfYearStart = now.AddMonths(-5).AddDays(-now.Day + 1); // 5 месяцев назад (с начала месяца)

                // Получаем все заказы за неделю
                var weekOrders = await db.Orders
                    .Where(o => o.Date >= weekStart && o.Date <= now)
                    .Include(o => o.Products)
                    .ToListAsync();


                // Получаем все заказы за полгода
                var halfYearOrders = await db.Orders
                    .Where(o => o.Date >= halfYearStart && o.Date <= now)
                    .Include(o => o.Products)
                    .ToListAsync();

                // Преобразуем данные для недели
                var weekData = weekOrders.GroupBy(o => o.Date.DayOfWeek)
                    .Select(g => new
                    {
                        Label = g.Key.ToString(), // Пн, Вт, Ср, ...
                        RevenueK = g.Sum(x => x.TotalPrice) / 1000,  // Выручка в тыс.₽
                        Units = g.Sum(x => x.Products.Sum(p => p.Quantity))  // Сумма проданных товаров
                    }).ToList();
                Console.WriteLine("Week Data:");
                foreach (var dayData in weekData)
                {
                    Console.WriteLine($"Day: {dayData.Label}, Revenue: {dayData.RevenueK}, Units Sold: {dayData.Units}");
                }
                // Преобразуем данные для полугода
                var halfYearData = halfYearOrders.GroupBy(o => new { o.Date.Year, o.Date.Month })
                    .Select(g => new
                    {
                        Label = $"{g.Key.Month:D2}.{g.Key.Year}",  // Месяц.Год
                        RevenueK = g.Sum(x => x.TotalPrice) / 1000,  // Выручка в тыс.₽
                        Units = g.Sum(x => x.Products.Sum(p => p.Quantity))  // Сумма проданных товаров
                    }).ToList();

                // Рассчитываем общую выручку за неделю
                var weekTotalRevenue = weekOrders.Sum(x => x.TotalPrice) / 1000;  // в тыс.₽

                // Рассчитываем общую выручку за полгода
                var halfYearTotalRevenue = halfYearOrders.Sum(x => x.TotalPrice) / 1000;  // в тыс.₽

                // Подготавливаем данные в формате для клиента
                var result = new
                {
                    halfYear = new
                    {
                        revenue = halfYearTotalRevenue,  // Выручка за полгода
                        categories = halfYearData.Select(d => d.Label).ToArray(),  // Месяцы
                        lineChartData = new List<object>
            {
                new
                {
                    name = "Выручка, тыс.₽",
                    data = halfYearData.Select(d => d.RevenueK).ToArray()
                },
                new
                {
                    name = "Продажи",
                    data = halfYearData.Select(d => d.Units).ToArray()
                }
            }.ToArray()
                    },
                    week = new
                    {
                        revenue = weekTotalRevenue,  // Выручка за неделю
                        categories = new[] { "вс", "вт", "пн", "пт", "сб", "чт" },  // Дни недели
                        lineChartData = new List<object>
            {
                new
                {
                    name = "Выручка, тыс.₽",
                    data = weekData.Select(d => d.RevenueK).ToArray()
                },
                new
                {
                    name = "Продажи",
                    data = weekData.Select(d => d.Units).ToArray()
                }
            }.ToArray()
                    }
                };

                return Results.Ok(result);
            });




            // 3. Pie chart популярных категорий
            group.MapGet("/popular-categories", async (AppDbContext db) =>
            {
                var now = DateTimeOffset.UtcNow;
                var start = now.AddMonths(-1);

                var categoryCounts = await db.OrderProducts
                    .Where(op => op.Order.Date >= start && op.Order.Date <= now)
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
            group.MapGet("/recent-sales", async (AppDbContext db) =>
            {
                var recentOrders = await db.Orders
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
            // 5. Топ 10
            group.MapGet("/top10-products", async (AppDbContext db) =>
            {
                var topProducts = await db.OrderProducts
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
            group.MapGet("/top10-clients", async (AppDbContext db) =>
            {
                var topClients = await db.Orders
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
            // 6. Получить все продукты
            group.MapGet("/all-products", async (AppDbContext db) =>
            {
                try
                {
                    var products = await db.Products
                        .Include(p => p.Category)  // Включаем связанные категории
                        .Select(p => new
                        {
                            id = p.Id,    // Идентификатор продукта
                            name = p.Name,         // Название продукта
                            category = p.Category.CategoryName,  // Название категории
                            weight = p.Weight,       // Вес продукта
                            price = p.Price,        // Цена продукта
                           
                        })
                        .ToListAsync();

                    return Results.Ok(products);
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Ошибка при получении продуктов: {ex.Message}");
                }
            });

            group.MapGet("/all-clients", async (AppDbContext db) =>
            {
                try
                {
                    var clients = await db.Clients
                        .Select(c => new
                        {
                            id = c.Id,
                            fullName = c.FullName,
                            phone = c.Phone,
                            address = c.Address,
                            cashback = c.Cashback
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
            group.MapDelete("/client/{id}", async (AppDbContext db, int id) =>
            {
                var client = await db.Clients.FindAsync(id);

                if (client == null)
                {
                    return Results.NotFound($"Клиент с ID {id} не найден.");
                }

                try
                {
                    db.Clients.Remove(client);
                    await db.SaveChangesAsync();
                    return Results.Ok($"Клиент с ID {id} был успешно удален.");
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Ошибка при удалении клиента: {ex.Message}");
                }
            });
            // Обновление данных клиента
            group.MapPut("/client/{id}", async (AppDbContext db, int id, ClientDTO clientDto) =>
            {
                var client = await db.Clients.FindAsync(id);

                if (client == null)
                {
                    return Results.NotFound($"Клиент с ID {id} не найден.");
                }

                try
                {
                    
                    client.FullName = clientDto.FullName;
                    client.Phone = clientDto.Phone;
                    client.Address = clientDto.Address;
                    client.Cashback = clientDto.Cashback;

                    await db.SaveChangesAsync();
                    return Results.Ok($"Клиент с ID {id} был успешно обновлен.");
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
                    // Проверка на обязательные поля
                    if (string.IsNullOrEmpty(clientDto.FullName) || string.IsNullOrEmpty(clientDto.Phone))
                    {
                        return Results.BadRequest("Пожалуйста, укажите корректные данные для клиента.");
                    }

                    // Создание нового клиента
                    var newClient = new Client
                    {
                        FullName = clientDto.FullName,
                        Phone = clientDto.Phone,
                        Address = clientDto.Address,
                        Cashback = clientDto.Cashback,
                        Comment = clientDto.Comment
                    };

                    // Добавление клиента в базу данных
                    db.Clients.Add(newClient);
                    await db.SaveChangesAsync();

                    // Возвращаем успешный результат с информацией о новом клиенте
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
                    // Ошибка при создании клиента
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
                var client = await db.Clients.FindAsync(dto.ClientId);
                if (client == null)
                    return Results.BadRequest("Клиент не найден.");

                if (client.Cashback < dto.CashbackUsed)
                    return Results.BadRequest("Недостаточно кешбэка.");

                var order = new Order
                {
                    ClientId = dto.ClientId,
                    Date = DateTimeOffset.UtcNow, // важное исправление
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

                // Загружаем все продукты из базы данных по списку ID
                var productIds = order.Products.Select(p => p.ProductId).ToList();

                var productsFromDb = await db.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToListAsync();

                // Вычисляем сумму
                var subtotal = productsFromDb.Sum(p =>
                {
                    var quantity = order.Products.First(op => op.ProductId == p.Id).Quantity;
                    return p.Price * quantity;
                });

                var discount = subtotal * ((decimal)order.DiscountPercent / 100m);
                var finalPrice = subtotal - discount - order.CashbackUsed;
                order.TotalPrice = finalPrice >= 0 ? finalPrice : 0;
                order.CashbackEarned = (subtotal - discount) * 0.05m;

                // Обновляем кешбэк клиента
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

        }
    }

}
