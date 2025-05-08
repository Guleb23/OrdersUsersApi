using Microsoft.EntityFrameworkCore;
using OrdersUsersApi.Context;
using OrdersUsersApi.DTO.Client;
using OrdersUsersApi.Models;

public static class ClientsEndpoints
{
    public static RouteGroupBuilder MapClientsEndpoints(this RouteGroupBuilder group)
    {
        // Все клиенты
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

        // Детальная информаия
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

        return group;
    }
}
