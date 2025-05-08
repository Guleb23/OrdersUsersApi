using Microsoft.EntityFrameworkCore;
using OrdersUsersApi.Context;

public static class MiniStatsEndpoints
{
    public static RouteGroupBuilder MapMiniStatsEndpoints(this RouteGroupBuilder group)
    {
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

        // 4. Топ-10 продуктов
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

        // 6. Последние продажи
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

        return group;
    }
}
