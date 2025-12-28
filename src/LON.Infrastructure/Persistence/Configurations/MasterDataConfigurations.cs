using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Items");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.HSCode).HasMaxLength(20);
        builder.Property(e => e.CountryOfOrigin).HasMaxLength(3);
        builder.Property(e => e.StandardCost).HasColumnType("decimal(18,4)");
        
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("UnitsOfMeasure");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Symbol).HasMaxLength(10);
        
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Address).HasMaxLength(500);
        
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("Locations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Aisle).HasMaxLength(20);
        builder.Property(e => e.Rack).HasMaxLength(20);
        builder.Property(e => e.Shelf).HasMaxLength(20);
        builder.Property(e => e.Bin).HasMaxLength(20);
        builder.Property(e => e.MaxCapacity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.CurrentCapacity).HasColumnType("decimal(18,4)");
        
        builder.HasIndex(e => new { e.WarehouseId, e.Code }).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.ToTable("Partners");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.TaxNumber).HasMaxLength(50);
        builder.Property(e => e.Address).HasMaxLength(500);
        builder.Property(e => e.ContactPerson).HasMaxLength(200);
        builder.Property(e => e.Email).HasMaxLength(100);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.Country).HasMaxLength(3);
        
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EmployeeNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Email).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Department).HasMaxLength(100);
        builder.Property(e => e.Position).HasMaxLength(100);
        
        builder.HasIndex(e => e.EmployeeNumber).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class WorkCenterConfiguration : IEntityTypeConfiguration<WorkCenter>
{
    public void Configure(EntityTypeBuilder<WorkCenter> builder)
    {
        builder.ToTable("WorkCenters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.StandardCostPerHour).HasColumnType("decimal(18,4)");
        builder.Property(e => e.Capacity).HasColumnType("decimal(18,4)");
        
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
