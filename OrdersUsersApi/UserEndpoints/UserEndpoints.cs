using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrdersUsersApi.Context;
using OrdersUsersApi.DTO.User;
using OrdersUsersApi.Helpers;
using OrdersUsersApi.Models;

namespace OrdersUsersApi.UserEndpoints
{
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/user").WithTags("User");

            // Регистрация пользователя
            group.MapPost("/register", async ([FromBody] RegisterDTO user, AppDbContext context, IConfiguration config) =>
            {
                // Проверяем, есть ли уже пользователь с таким email
                var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
                if (existingUser != null)
                {
                    return Results.Conflict("Пользователь с таким email уже зарегистрирован");
                }

                // Создаем нового пользователя
                User newUser = new User()
                {
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    // Хешируем пароль перед сохранением
                    Password = BCrypt.Net.BCrypt.HashPassword(user.Password)
                };

                context.Users.Add(newUser);
                await context.SaveChangesAsync();

                // Генерируем JWT токен
                var token = JwtHelper.GenerateToken(newUser.Email, config);

                // Возвращаем токен и данные пользователя (без пароля)
                return Results.Ok(new
                {
                    token,
                    user = new
                    {
                        newUser.Id,
                        newUser.Email,
                        newUser.FirstName,
                        newUser.LastName,
                        newUser.CashbackPercent
                    }
                });
            });

            // Авторизация пользователя
            group.MapPost("/login", async ([FromBody] LoginDTO loginDto, AppDbContext context, IConfiguration config) =>
            {
                // Ищем пользователя по email
                var user = await context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);
                // Проверяем пароль с помощью BCrypt
                if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.Password))
                    return Results.Unauthorized(); // Неверный email или пароль

                // Генерируем JWT токен
                var token = JwtHelper.GenerateToken(user.Email, config);

                // Возвращаем токен и данные пользователя (без пароля)
                return Results.Ok(new
                {
                    token,
                    user = new { user.Id, user.Email, user.FirstName, user.LastName, user.CashbackPercent }
                });
            });

            // Обновление данных пользователя
            group.MapPut("/update/{id}", async ([FromRoute] int id, [FromBody] UpdateUserDTO updatedUser, AppDbContext context) =>
            {
                // Находим пользователя
                var user = await context.Users.FindAsync(id);
                if (user == null)
                    return Results.NotFound("Пользователь не найден");

                // Проверяем старый пароль
                if (!BCrypt.Net.BCrypt.Verify(updatedUser.OldPassword, user.Password))
                    return Results.BadRequest("Старый пароль неверный");

                // Обновляем данные
                user.FirstName = updatedUser.FirstName;
                user.LastName = updatedUser.LastName;
                user.Email = updatedUser.Email;
                user.CashbackPercent = updatedUser.cashbackPercent;

                // Если указан новый пароль - хешируем и сохраняем
                if (!string.IsNullOrEmpty(updatedUser.NewPassword))
                {
                    user.Password = BCrypt.Net.BCrypt.HashPassword(updatedUser.NewPassword);
                }

                await context.SaveChangesAsync();
                return Results.Ok("Профиль успешно обновлён");
            });
            //  Получение данных пользователя
            group.MapGet("/getuser/{userId}", async ([FromRoute] int userId, AppDbContext context) =>
            {
                var user = context.Users.Find(userId);
                return user;
            });
            //  Удаление данных пользователя
            group.MapDelete("/delete/{id}", async ([FromRoute] int id, AppDbContext context) =>
            {
                var user = await context.Users.FindAsync(id);
                if (user == null)
                    return Results.NotFound("Пользователь не найден");

                // Удаление клиентов пользователя
                var clients = await context.Clients.Where(c => c.UserId == id).ToListAsync();
                foreach (var client in clients)
                {
                    // Удаление заказов клиента
                    var orders = await context.Orders
                        .Where(o => o.ClientId == client.Id)
                        .Include(o => o.Products)
                        .ToListAsync();

                    foreach (var order in orders)
                    {
                        context.OrderProducts.RemoveRange(order.Products); // связанные товары
                    }

                    context.Orders.RemoveRange(orders);
                }

                context.Clients.RemoveRange(clients);

                // Можно добавить другие связанные сущности, если они есть

                context.Users.Remove(user);
                await context.SaveChangesAsync();

                return Results.Ok("Пользователь и все связанные данные успешно удалены");
            });
        }
    }
}
