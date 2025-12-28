using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class DeclarationRuleConfiguration : IEntityTypeConfiguration<DeclarationRule>
{
    public void Configure(EntityTypeBuilder<DeclarationRule> builder)
    {
        builder.ToTable("DeclarationRules");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.RuleCode)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.HasIndex(x => x.RuleCode)
            .IsUnique();
        
        builder.Property(x => x.FieldName)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(x => x.RuleType)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();
        
        builder.Property(x => x.ValidationLogic)
            .IsRequired()
            .HasMaxLength(1000);
        
        builder.Property(x => x.ErrorMessageMK)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(x => x.ErrorMessageEN)
            .HasMaxLength(500);
        
        builder.Property(x => x.Severity)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();
        
        builder.Property(x => x.ProcedureCode)
            .HasMaxLength(10);
        
        builder.HasIndex(x => new { x.FieldName, x.ProcedureCode });
    }
}
