using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(User.MaxEmailLength).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(User.MaxFullNameLength).IsRequired();
        builder.Property(u => u.Phone).HasMaxLength(20).IsRequired();
        builder.HasIndex(u => u.Phone);
        builder.Property(u => u.Status).IsRequired();

        builder.Property(u => u.AffiliateCode).HasMaxLength(12);
        builder.HasIndex(u => u.AffiliateCode).IsUnique();
        builder.Property(u => u.BlockedReason).HasMaxLength(User.MaxBlockReasonLength);
        builder.Property(u => u.AffiliateCodeDisabledReason).HasMaxLength(User.MaxBlockReasonLength);

        // Role[] stored as a PostgreSQL integer[] column.
        builder.PrimitiveCollection(u => u.Roles)
            .HasField("_roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        builder.Ignore(u => u.DomainEvents);
    }
}
