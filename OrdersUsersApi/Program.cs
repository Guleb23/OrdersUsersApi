using OrdersUsersApi.ServerConfig;

namespace OrdersUsersApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.ConfigureAppServices(builder.Configuration);

            try
            {
                var app = builder.Build();
                var logger = app.Services
                .GetRequiredService<ILogger<Program>>();
                app.ApplyMigrationsAndSeed();
                app.ConfigureApp();
                app.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Критическая ошибка запуска " + ex.ToString());
                throw;
            }
        }
    }
}
