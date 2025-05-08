using OrdersUsersApi.DashboardMain;
using OrdersUsersApi.UserEndpoints;

namespace OrdersUsersApi.ServerConfig
{
    public static class AppBuilderExtensions
    {
        public static void ConfigureApp(this WebApplication app)
        {
            app.UseCors("AllowAllOrigins");

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.Urls.Add("http://*:5000");
            app.MapGet("/", () => "server is running");

            app.MapDashboardEndpoints();
            app.MapUserEndpoints();
        }
    }
}
