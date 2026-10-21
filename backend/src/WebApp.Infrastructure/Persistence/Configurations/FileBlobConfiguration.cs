using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;
using WebApp.Infrastructure.Storage;

namespace WebApp.Infrastructure.Persistence.Configurations;

/// <summary>Uploaded files kept in the database when <c>Storage:Provider=Database</c> (ADR 0006).</summary>
internal sealed class FileBlobConfiguration : IEntityTypeConfiguration<FileBlob>
{
    public void Configure(EntityTypeBuilder<FileBlob> builder)
    {
        builder.ToTable("file_blobs");
        builder.HasKey(f => f.Path);
        builder.Property(f => f.Path).HasMaxLength(StoredImage.MaxPathLength);
        builder.Property(f => f.Data).IsRequired();
    }
}
