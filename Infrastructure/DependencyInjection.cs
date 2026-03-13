using Application.Interfaces;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Security;
using Infrastructure.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System;
using System.Text;

namespace Infrastructure;

/// <summary>
/// Dang ky Infrastructure: DbContext (PostgreSQL), Application services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                   .UseSnakeCaseNamingConvention();
        });

        services.AddCors(options =>
        {
            options.AddPolicy("AllowFEApp",
                policy =>
                {
                    policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
        });

        // Map PostgreSQL enum types (ten phai trung schema: order_status, payment_status, ...)
        NpgsqlConnection.GlobalTypeMapper.MapEnum<OrderStatus>("order_status");
        NpgsqlConnection.GlobalTypeMapper.MapEnum<PaymentStatus>("payment_status");
        NpgsqlConnection.GlobalTypeMapper.MapEnum<PaymentMethod>("payment_method");
        NpgsqlConnection.GlobalTypeMapper.MapEnum<ShipmentStatus>("shipment_status");

        //Cấu hình Jwt
        //Đăng ký dịch vụ tạo JWT token
        services.AddScoped<IJwtTokenGenerator, JwtUtils>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        var jwtSettings = configuration.GetSection("Jwt");
        var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };
        });

        //Services
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IBrandService, BrandService>();

        //Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IBrandRepository, BrandRepository>();


        return services;
    }
}
