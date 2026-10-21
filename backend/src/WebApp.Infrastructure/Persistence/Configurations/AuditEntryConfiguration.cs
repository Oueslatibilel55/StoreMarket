using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ActorEmail).HasMaxLength(User.MaxEmailLength).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(AuditEntry.MaxActionLength).IsRequired();
        builder.Property(a => a.TargetType).HasMaxLength(32).IsRequired();
        builder.Property(a => a.TargetLabel).HasMaxLength(254);
        builder.Property(a => a.Reason).HasMaxLength(AuditEntry.MaxReasonLength);
        builder.Property(a => a.Before).HasColumnType("jsonb");
        builder.Property(a => a.After).HasColumnType("jsonb");

        builder.HasIndex(a => new { a.TargetType, a.TargetId, a.CreatedAt });
        builder.HasIndex(a => new { a.ActorId, a.CreatedAt });
        builder.HasIndex(a => a.CreatedAt);
    }
}
