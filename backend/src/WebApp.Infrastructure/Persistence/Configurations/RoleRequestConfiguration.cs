using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class RoleRequestConfiguration : IEntityTypeConfiguration<RoleRequest>
{
    public void Configure(EntityTypeBuilder<RoleRequest> builder)
    {
        builder.ToTable("role_requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Role).IsRequired();
        builder.Property(r => r.Status).IsRequired();
        builder.Property(r => r.Message).HasMaxLength(RoleRequest.MaxMessageLength);
        builder.Property(r => r.DecisionReason).HasMaxLength(RoleRequest.MaxReasonLength);

        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.DecidedById).OnDelete(DeleteBehavior.Restrict);

        // At most one pending request per user and role — the service checks first, this is the backstop.
        builder.HasIndex(r => new { r.UserId, r.Role })
            .IsUnique()
            .HasFilter($"status = {(int)RoleRequestStatus.Pending}");
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
        builder.HasIndex(r => new { r.UserId, r.CreatedAt });

        builder.Ignore(r => r.DomainEvents);
    }
}
