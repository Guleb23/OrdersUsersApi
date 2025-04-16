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

            group.MapPost("/register", async ([FromBody] RegisterDTO user, AppDbContext context, IConfiguration config) =>
            {
                var exsitingUser = context.Users.FirstOrDefault(u => u.Email == user.Email);
                if (exsitingUser != null) 
                {
                    return Results.Conflict("Пользователь с таким email уже зарегестрирован");
                }
                else
                {
                    User newUser = new User()
                    {
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Password = user.Password,
                    };

                    
                    context.Users.Add(newUser);
                    var token = JwtHelper.GenerateToken(newUser.Email, config);
                    await context.SaveChangesAsync();
                    
                    return Results.Ok(new{
                        token,
                        user = newUser
                    });

                    }
                
                });

            group.MapPost("/login", async ([FromBody] LoginDTO loginDto, AppDbContext context, IConfiguration config) =>
            {
                var user = await context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

                if (user == null || user.Password != loginDto.Password)
                    return Results.Unauthorized();

                var token = JwtHelper.GenerateToken(user.Email, config);

                return Results.Ok(new
                {
                    token,
                    user = new { user.Id, user.Email, user.FirstName, user.LastName, user.CashbackPercent }
                });
            });
            group.MapPut("/update/{id}", async ([FromRoute] int id, [FromBody] UpdateUserDTO updatedUser, AppDbContext context) =>
            {
                var user = await context.Users.FindAsync(id);
                if (user == null)
                    return Results.NotFound("Пользователь не найден");

                if (user.Password != updatedUser.OldPassword)
                    return Results.BadRequest("Старый пароль неверный");

                user.FirstName = updatedUser.FirstName;
                user.LastName = updatedUser.LastName;
                user.Email = updatedUser.Email;
                user.CashbackPercent = updatedUser.cashbackPercent;

                if (!string.IsNullOrEmpty(updatedUser.NewPassword))
                {
                    user.Password = updatedUser.NewPassword;
                }

                await context.SaveChangesAsync();
                return Results.Ok("Профиль успешно обновлён");
            });

            group.MapGet("/getuser/{userId}", async ([FromRoute] int userId, AppDbContext context) =>
            {
                var user = context.Users.Find(userId);
                return user;
            });
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
