using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class UserFieldHistoryConfiguration : IEntityTypeConfiguration<UserFieldHistory>
{
    public void Configure(EntityTypeBuilder<UserFieldHistory> builder)
    {
        builder.ToTable("UserFieldHistories");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FieldKey).IsRequired().HasMaxLength(128);
        builder.Property(e => e.Value).IsRequired().HasMaxLength(512);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Primary read path: (UserId, FieldKey) ordered by LastUsedAt DESC.
        // Filtered unique index on (UserId, FieldKey, Value) prevents duplicate
        // "recent" rows — upsert updates in place.
        builder.HasIndex(e => new { e.UserId, e.FieldKey, e.LastUsedAt });
        builder.HasIndex(e => new { e.UserId, e.FieldKey, e.Value })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
