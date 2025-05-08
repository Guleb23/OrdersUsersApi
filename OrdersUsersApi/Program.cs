
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using OrdersUsersApi.Context;
using OrdersUsersApi.DashboardMain;
using System.Text;
using OrdersUsersApi.UserEndpoints;

namespace OrdersUsersApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthentication("Bearer")
     .AddJwtBearer("Bearer", options =>
     {
         var config = builder.Configuration;
         options.TokenValidationParameters = new TokenValidationParameters
         {
             ValidateIssuer = true,
             ValidIssuer = config["Jwt:Issuer"],
             ValidateAudience = true,
             ValidAudience = config["Jwt:Audience"],
             ValidateLifetime = true,
             ValidateIssuerSigningKey = true,
             IssuerSigningKey = new SymmetricSecurityKey(
                 Encoding.UTF8.GetBytes(config["Jwt:Key"]!)
             )
         };
     });

            builder.Services.AddAuthorization();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    builder =>
                    {
                        builder.WithOrigins(
                                "http://localhost:3000", "https://test-server-psi-liart.vercel.app"

                            )
                           .AllowAnyMethod()
                           .AllowAnyHeader()
                           .AllowCredentials();
                    });
            });
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
            });
            var app = builder.Build();
            app.UseCors("AllowAllOrigins");
            //app.Urls.Add("http://*:5000");
            app.MapDashboardEndpoints();
            app.MapUserEndpoints();
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate(); // Применение миграций
                DbInitializer.Seed(db); // Заполнение
            }
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapGet("/", () =>
            {
                return "server is running";
            });
            app.Run();
        }
    }
}
