using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrdersUsersApi.Context;
using System.Text;

namespace OrdersUsersApi.ServerConfig
{
    public static class ServiceExtensions
    {
        public static void ConfigureAppServices(this IServiceCollection services, IConfiguration config)
        {
            services.AddAuthentication("Bearer")
                    .AddJwtBearer("Bearer", options =>
                    {
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

            services.AddAuthorization();

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    builder =>
                    {
                        builder.WithOrigins(
                            "http://localhost:3000",
                            "https://test-server-psi-liart.vercel.app")
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials();
                    });
            });

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(config.GetConnectionString("DefaultConnection")));
        }
    }
}
