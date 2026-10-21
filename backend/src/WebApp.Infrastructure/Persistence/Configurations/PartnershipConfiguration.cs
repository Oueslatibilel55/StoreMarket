using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class PartnershipConfiguration : IEntityTypeConfiguration<Partnership>
{
    public void Configure(EntityTypeBuilder<Partnership> builder)
    {
        builder.ToTable("partnerships");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status).IsRequired();
        builder.Property(p => p.Message).HasMaxLength(Partnership.MaxMessageLength);
        builder.Property(p => p.DecisionReason).HasMaxLength(Partnership.MaxReasonLength);
        builder.Property(p => p.EndReason).HasMaxLength(Partnership.MaxReasonLength);

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.AffiliateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Store>().WithMany().HasForeignKey(p => p.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.EndedById).OnDelete(DeleteBehavior.Restrict);

        // At most one requested or accepted partnership per affiliate and store — the service checks first, this is the backstop.
        builder.HasIndex(p => new { p.AffiliateId, p.StoreId })
            .IsUnique()
            .HasFilter($"status IN ({(int)PartnershipStatus.Requested}, {(int)PartnershipStatus.Accepted})");
        builder.HasIndex(p => new { p.StoreId, p.Status });
        builder.HasIndex(p => new { p.AffiliateId, p.CreatedAt });

        builder.Ignore(p => p.DomainEvents);
    }
}
