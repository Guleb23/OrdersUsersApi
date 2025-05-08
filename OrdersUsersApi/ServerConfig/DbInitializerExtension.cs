using Microsoft.EntityFrameworkCore;
using OrdersUsersApi.Context;

namespace OrdersUsersApi.ServerConfig
{
    public static class DbInitializerExtension
    {
        public static void ApplyMigrationsAndSeed(this IHost app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
            DbInitializer.Seed(db);
        }
    }
}
