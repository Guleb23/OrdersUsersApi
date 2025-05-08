using OrdersUsersApi.Context;
using OrdersUsersApi.DTO.Catrgory;
using OrdersUsersApi.Models;

public static class CategoriesEndpoints
{
    public static RouteGroupBuilder MapCategoriesEndpoints(this RouteGroupBuilder group)
    {
        //Получение всех категорий
        group.MapGet("/categories", async (AppDbContext db) =>
        {
            return db.ProductCategories.ToList();
        });

        //Добавление новой категории
        group.MapPost("/createCategory", async (AppDbContext db, CategoryDTO dto) =>
        {
            var exstinCat = db.ProductCategories.FirstOrDefault(c => c.CategoryName == dto.name);
            if (exstinCat == null)
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

        return group;
    }
}
