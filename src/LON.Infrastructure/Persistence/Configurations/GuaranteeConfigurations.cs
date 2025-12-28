using LON.Domain.Entities.Guarantee;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class GuaranteeAccountConfiguration : IEntityTypeConfiguration<GuaranteeAccount>
{
    public void Configure(EntityTypeBuilder<GuaranteeAccount> builder)
    {
        builder.ToTable("GuaranteeAccounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.AccountNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.AccountName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.TotalLimit).HasColumnType("decimal(18,4)");
        builder.Property(e => e.Notes).HasMaxLength(500);
        
        builder.HasIndex(e => e.AccountNumber).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
        
        builder.Ignore(e => e.LedgerEntries);
    }
}

public class GuaranteeLedgerEntryConfiguration : IEntityTypeConfiguration<GuaranteeLedgerEntry>
{
    public void Configure(EntityTypeBuilder<GuaranteeLedgerEntry> builder)
    {
        builder.ToTable("GuaranteeLedgerEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Amount).HasColumnType("decimal(18,4)");
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.ReferenceType).HasMaxLength(50);
        builder.Property(e => e.MRN).HasMaxLength(100);
        builder.Property(e => e.Notes).HasMaxLength(500);
        
        builder.HasIndex(e => e.GuaranteeAccountId);
        builder.HasIndex(e => e.EntryDate);
        builder.HasIndex(e => e.MRN);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class DutyCalculationConfiguration : IEntityTypeConfiguration<DutyCalculation>
{
    public void Configure(EntityTypeBuilder<DutyCalculation> builder)
    {
        builder.ToTable("DutyCalculations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.HSCode).HasMaxLength(20);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.CustomsValue).HasColumnType("decimal(18,4)");
        builder.Property(e => e.DutyRate).HasColumnType("decimal(18,4)");
        builder.Property(e => e.DutyAmount).HasColumnType("decimal(18,4)");
        builder.Property(e => e.VATRate).HasColumnType("decimal(18,4)");
        builder.Property(e => e.VATAmount).HasColumnType("decimal(18,4)");
        builder.Property(e => e.OtherCharges).HasColumnType("decimal(18,4)");
        builder.Property(e => e.TotalAmount).HasColumnType("decimal(18,4)");
        builder.Property(e => e.Notes).HasMaxLength(500);
        
        builder.HasIndex(e => e.CustomsDeclarationId);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
