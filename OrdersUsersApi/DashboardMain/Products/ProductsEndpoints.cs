using Microsoft.EntityFrameworkCore;
using OrdersUsersApi.Context;
using OrdersUsersApi.DTO.Products;
using OrdersUsersApi.Models;

public static class ProductsEndpoints
{
    public static RouteGroupBuilder MapProductsEndpoints(this RouteGroupBuilder group)
    {
        //Все продукты
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

        return group;
    }
}
