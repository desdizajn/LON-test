using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class EmployeeCertificationConfiguration : IEntityTypeConfiguration<EmployeeCertification>
{
    public void Configure(EntityTypeBuilder<EmployeeCertification> builder)
    {
        builder.ToTable("EmployeeCertifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CertificationName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.SkillArea).HasMaxLength(60);
        builder.Property(x => x.IssuingAuthority).HasMaxLength(120);
        builder.Property(x => x.CertificateNumber).HasMaxLength(60);
        builder.Property(x => x.Notes).HasColumnType("nvarchar(max)");

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.EmployeeId })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_EmployeeCertifications_TenantId_EmployeeId");

        builder.HasIndex(x => x.ExpiryDate)
            .HasFilter("[IsDeleted] = 0 AND [ExpiryDate] IS NOT NULL")
            .HasDatabaseName("IX_EmployeeCertifications_ExpiryDate");
    }
}
