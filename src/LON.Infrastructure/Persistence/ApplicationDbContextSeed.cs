using LON.Domain.Entities.Customs;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // 1. Seed Knowledge Base податоци (TARIC, Regulations, CodeLists, DeclarationRules)
        await KnowledgeBaseSeeder.SeedKnowledgeBaseAsync(context);

        // 2. Seed Master Data
        if (!await context.UnitsOfMeasure.AnyAsync())
        {
            await SeedUnitsOfMeasure(context);
        }

        if (!await context.Items.AnyAsync())
        {
            await SeedItems(context);
        }

        if (!await context.Warehouses.AnyAsync())
        {
            await SeedWarehouses(context);
        }

        if (!await context.Partners.AnyAsync())
        {
            await SeedPartners(context);
        }

        if (!await context.Employees.AnyAsync())
        {
            await SeedEmployees(context);
        }

        if (!await context.WorkCenters.AnyAsync())
        {
            await SeedWorkCenters(context);
        }

        if (!await context.CustomsProcedures.AnyAsync())
        {
            await SeedCustomsProcedures(context);
        }

        if (!await context.GuaranteeAccounts.AnyAsync())
        {
            await SeedGuaranteeAccounts(context);
        }
    }

    private static async Task SeedUnitsOfMeasure(ApplicationDbContext context)
    {
        var uoms = new[]
        {
            new UnitOfMeasure { Id = Guid.NewGuid(), Code = "PCS", Name = "Pieces", Symbol = "pcs", CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
            new UnitOfMeasure { Id = Guid.NewGuid(), Code = "KG", Name = "Kilogram", Symbol = "kg", CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
            new UnitOfMeasure { Id = Guid.NewGuid(), Code = "L", Name = "Liter", Symbol = "L", CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
            new UnitOfMeasure { Id = Guid.NewGuid(), Code = "M", Name = "Meter", Symbol = "m", CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
            new UnitOfMeasure { Id = Guid.NewGuid(), Code = "BOX", Name = "Box", Symbol = "box", CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
            new UnitOfMeasure { Id = Guid.NewGuid(), Code = "PAL", Name = "Pallet", Symbol = "pal", CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
        };

        await context.UnitsOfMeasure.AddRangeAsync(uoms);
        await context.SaveChangesAsync();
    }

    private static async Task SeedItems(ApplicationDbContext context)
    {
        var uomPcs = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "PCS");
        var uomKg = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "KG");

        var items = new[]
        {
            new Item
            {
                Id = Guid.NewGuid(),
                Code = "RM-001",
                Name = "Raw Material A",
                Description = "Primary raw material for production",
                Type = ItemType.RawMaterial,
                IsBatchTracked = true,
                IsMRNTracked = true,
                HSCode = "3901.10",
                CountryOfOrigin = "DEU",
                BaseUoMId = uomKg.Id,
                StandardCost = 15.50m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new Item
            {
                Id = Guid.NewGuid(),
                Code = "RM-002",
                Name = "Raw Material B",
                Description = "Secondary raw material",
                Type = ItemType.RawMaterial,
                IsBatchTracked = true,
                IsMRNTracked = true,
                HSCode = "3902.10",
                CountryOfOrigin = "ITA",
                BaseUoMId = uomKg.Id,
                StandardCost = 12.30m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new Item
            {
                Id = Guid.NewGuid(),
                Code = "SF-001",
                Name = "Semi-Finished Product",
                Description = "Intermediate product",
                Type = ItemType.SemiFinished,
                IsBatchTracked = true,
                IsMRNTracked = false,
                HSCode = "3920.10",
                CountryOfOrigin = "MKD",
                BaseUoMId = uomPcs.Id,
                StandardCost = 45.00m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new Item
            {
                Id = Guid.NewGuid(),
                Code = "FG-001",
                Name = "Finished Good A",
                Description = "Final product for export",
                Type = ItemType.FinishedGood,
                IsBatchTracked = true,
                IsMRNTracked = false,
                HSCode = "3926.90",
                CountryOfOrigin = "MKD",
                BaseUoMId = uomPcs.Id,
                StandardCost = 89.50m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new Item
            {
                Id = Guid.NewGuid(),
                Code = "PKG-001",
                Name = "Cardboard Box",
                Description = "Packaging material",
                Type = ItemType.Packaging,
                IsBatchTracked = false,
                IsMRNTracked = false,
                BaseUoMId = uomPcs.Id,
                StandardCost = 2.50m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            }
        };

        await context.Items.AddRangeAsync(items);
        await context.SaveChangesAsync();
    }

    private static async Task SeedWarehouses(ApplicationDbContext context)
    {
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "WH-MAIN",
            Name = "Main Warehouse",
            Address = "Industrial Zone, Skopje",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Seed"
        };

        await context.Warehouses.AddAsync(warehouse);
        await context.SaveChangesAsync();

        var locations = new[]
        {
            new Location { Id = Guid.NewGuid(), Code = "RCV-01", Name = "Receiving Zone 1", WarehouseId = warehouse.Id, Type = LocationType.Receiving, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
            new Location { Id = Guid.NewGuid(), Code = "STG-A-01", Name = "Storage Aisle A Rack 01", WarehouseId = warehouse.Id, Type = LocationType.Storage, Aisle = "A", Rack = "01", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
            new Location { Id = Guid.NewGuid(), Code = "STG-A-02", Name = "Storage Aisle A Rack 02", WarehouseId = warehouse.Id, Type = LocationType.Storage, Aisle = "A", Rack = "02", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
            new Location { Id = Guid.NewGuid(), Code = "PICK-01", Name = "Picking Zone 1", WarehouseId = warehouse.Id, Type = LocationType.Picking, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
            new Location { Id = Guid.NewGuid(), Code = "PROD-01", Name = "Production Floor", WarehouseId = warehouse.Id, Type = LocationType.Production, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
            new Location { Id = Guid.NewGuid(), Code = "SHIP-01", Name = "Shipping Zone", WarehouseId = warehouse.Id, Type = LocationType.Shipping, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
            new Location { Id = Guid.NewGuid(), Code = "QUA-01", Name = "Quarantine", WarehouseId = warehouse.Id, Type = LocationType.Quarantine, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "Seed" },
        };

        await context.Locations.AddRangeAsync(locations);
        await context.SaveChangesAsync();
    }

    private static async Task SeedPartners(ApplicationDbContext context)
    {
        var partners = new[]
        {
            new Partner
            {
                Id = Guid.NewGuid(),
                Code = "SUP-001",
                Name = "German Supplier GmbH",
                Type = PartnerType.Supplier,
                TaxNumber = "DE123456789",
                Address = "Berlin, Germany",
                Country = "DEU",
                Email = "contact@supplier.de",
                Phone = "+49-30-12345678",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new Partner
            {
                Id = Guid.NewGuid(),
                Code = "CUS-001",
                Name = "Italian Customer SRL",
                Type = PartnerType.Customer,
                TaxNumber = "IT98765432100",
                Address = "Milano, Italy",
                Country = "ITA",
                Email = "orders@customer.it",
                Phone = "+39-02-87654321",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new Partner
            {
                Id = Guid.NewGuid(),
                Code = "CAR-001",
                Name = "Express Logistics",
                Type = PartnerType.Carrier,
                Address = "Skopje, North Macedonia",
                Country = "MKD",
                Email = "dispatch@logistics.mk",
                Phone = "+389-2-1234567",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new Partner
            {
                Id = Guid.NewGuid(),
                Code = "BANK-001",
                Name = "National Bank",
                Type = PartnerType.Bank,
                Address = "Skopje, North Macedonia",
                Country = "MKD",
                Email = "corporate@bank.mk",
                Phone = "+389-2-9876543",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            }
        };

        await context.Partners.AddRangeAsync(partners);
        await context.SaveChangesAsync();
    }

    private static async Task SeedEmployees(ApplicationDbContext context)
    {
        var employees = new[]
        {
            new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeNumber = "EMP-001",
                FirstName = "Marko",
                LastName = "Petrovski",
                Email = "marko.petrovski@company.mk",
                Department = "Warehouse",
                Position = "Warehouse Manager",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeNumber = "EMP-002",
                FirstName = "Ana",
                LastName = "Jovanovska",
                Email = "ana.jovanovska@company.mk",
                Department = "Production",
                Position = "Production Manager",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeNumber = "EMP-003",
                FirstName = "Stefan",
                LastName = "Nikoloski",
                Email = "stefan.nikoloski@company.mk",
                Department = "Customs",
                Position = "Customs Officer",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            }
        };

        await context.Employees.AddRangeAsync(employees);
        await context.SaveChangesAsync();
    }

    private static async Task SeedWorkCenters(ApplicationDbContext context)
    {
        var workCenters = new[]
        {
            new WorkCenter
            {
                Id = Guid.NewGuid(),
                Code = "WC-001",
                Name = "Assembly Line 1",
                Description = "Main assembly line",
                StandardCostPerHour = 50.00m,
                Capacity = 100.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new WorkCenter
            {
                Id = Guid.NewGuid(),
                Code = "WC-002",
                Name = "Packaging Station",
                Description = "Final packaging",
                StandardCostPerHour = 30.00m,
                Capacity = 150.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            }
        };

        await context.WorkCenters.AddRangeAsync(workCenters);
        await context.SaveChangesAsync();
    }

    private static async Task SeedCustomsProcedures(ApplicationDbContext context)
    {
        var procedures = new[]
        {
            new CustomsProcedure
            {
                Id = Guid.NewGuid(),
                Code = "LOCAL",
                Name = "Local Purchase",
                Type = CustomsProcedureType.LocalPurchase,
                Description = "Purchase from domestic supplier",
                RequiresGuarantee = false,
                GuaranteePercentage = 0,
                RequiresMRNTracking = false,
                AllowsProduction = true,
                AllowsExport = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new CustomsProcedure
            {
                Id = Guid.NewGuid(),
                Code = "TEMP-IMP",
                Name = "Temporary Import",
                Type = CustomsProcedureType.TemporaryImport,
                Description = "Temporary import with guarantee",
                RequiresGuarantee = true,
                GuaranteePercentage = 100,
                DueDays = 365,
                RequiresMRNTracking = true,
                AllowsProduction = false,
                AllowsExport = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new CustomsProcedure
            {
                Id = Guid.NewGuid(),
                Code = "INW-PROC",
                Name = "Inward Processing",
                Type = CustomsProcedureType.InwardProcessing,
                Description = "Import for processing and re-export",
                RequiresGuarantee = true,
                GuaranteePercentage = 50,
                DueDays = 180,
                RequiresMRNTracking = true,
                AllowsProduction = true,
                AllowsExport = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new CustomsProcedure
            {
                Id = Guid.NewGuid(),
                Code = "FINAL",
                Name = "Final Clearance",
                Type = CustomsProcedureType.FinalClearance,
                Description = "Final import clearance with full duty payment",
                RequiresGuarantee = false,
                GuaranteePercentage = 0,
                RequiresMRNTracking = true,
                AllowsProduction = true,
                AllowsExport = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new CustomsProcedure
            {
                Id = Guid.NewGuid(),
                Code = "EXPORT",
                Name = "Export",
                Type = CustomsProcedureType.Export,
                Description = "Export procedure",
                RequiresGuarantee = false,
                GuaranteePercentage = 0,
                RequiresMRNTracking = true,
                AllowsProduction = false,
                AllowsExport = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            }
        };

        await context.CustomsProcedures.AddRangeAsync(procedures);
        await context.SaveChangesAsync();
    }

    private static async Task SeedGuaranteeAccounts(ApplicationDbContext context)
    {
        var bank = await context.Partners.FirstAsync(p => p.Code == "BANK-001");

        var accounts = new[]
        {
            new GuaranteeAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = "GUA-2024-001",
                AccountName = "Main Guarantee Account EUR",
                BankPartnerId = bank.Id,
                Currency = "EUR",
                TotalLimit = 500000.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            },
            new GuaranteeAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = "GUA-2024-002",
                AccountName = "Secondary Guarantee Account USD",
                BankPartnerId = bank.Id,
                Currency = "USD",
                TotalLimit = 300000.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            }
        };

        await context.GuaranteeAccounts.AddRangeAsync(accounts);
        await context.SaveChangesAsync();
    }
}
