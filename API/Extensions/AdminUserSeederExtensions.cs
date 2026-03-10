using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Helpers;
using Infrastructure.Persistence;
using Infrastructure.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace API.Extensions;

public static class AdminUserSeederExtensions
{
    public static async Task SeedAdminUserAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var configuration = services.GetRequiredService<IConfiguration>();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher>();

        await dbContext.Database.MigrateAsync();

        var adminSection = configuration.GetSection("AdminUser");
        var adminEmail = adminSection["Email"];
        var adminPassword = adminSection["Password"];
        var adminUserName = adminSection["UserName"] ?? "admin";
        var adminFullName = adminSection["FullName"] ?? "Administrator";

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var existingAdmin = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == adminEmail);

        if (existingAdmin != null)
        {
            return;
        }

        var adminRoleId = RoleHelper.GetId(UserRole.RoleAdmin);

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = adminEmail,
            Username = adminUserName,
            FullName = adminFullName,
            PasswordHash = passwordHasher.Hash(adminPassword),
            Status = 1,
            RoleId = adminRoleId
        };

        dbContext.Users.Add(adminUser);
        await dbContext.SaveChangesAsync();
    }
}

