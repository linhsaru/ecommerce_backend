using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

/// <summary>
/// Cau hinh entity User: table name, index, required.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasIndex(u => u.Email).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(u => u.Uuid).IsUnique();
    }
}
