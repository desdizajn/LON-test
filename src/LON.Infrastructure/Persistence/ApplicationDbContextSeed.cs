using LON.Domain.Entities.Customs;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    public static async Task SeedAsync(ApplicationDbContext context, bool skipKnowledgeBase = false)
    {
        // 1. Seed Knowledge Base податоци (TARIC, Regulations, CodeLists, DeclarationRules)
        if (!skipKnowledgeBase)
        {
            await KnowledgeBaseSeeder.SeedKnowledgeBaseAsync(context);
        }

        // 2. Seed Master Data
        if (!await context.Tenants.AnyAsync())
        {
            await SeedTenants(context);
        }

        if (!await context.UnitsOfMeasure.AnyAsync())
        {
            await SeedUnitsOfMeasure(context);
        }

        if (!await context.Items.AnyAsync())
        {
            await SeedItems(context);
        }

        // Warehouses are seeded per-code idempotently so new sites (e.g. TEKSPORT
        // Vinica) can be added across releases without dropping existing data.
        await SeedWarehousesIdempotent(context);

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

        // P2.1: seed a TEKSPORT LON authorization so IM 4200 declarations have
        // a valid `LONAuthorizationId` out of the box. Idempotent via
        // AuthorizationNumber lookup.
        await SeedTeksportLONAuthorizationIdempotent(context);

        // I1 idempotent backfill: ensure TEKSPORT inflate-for-waste is on
        // (existing DBs upgraded from earlier versions where the column
        // didn't exist default to false from the migration defaultValue).
        await BackfillTeksportInflateFlagAsync(context);
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

    /// <summary>
    /// Seed the Tenants table. Called separately from Program.cs BEFORE user
    /// management seed so that new users/items/etc. get a valid TenantId via
    /// the SaveChangesAsync auto-fill.
    /// </summary>
    public static async Task SeedTenantsAsync(ApplicationDbContext context)
    {
        if (await context.Tenants.AnyAsync()) return;
        await SeedTenantsInternal(context);
    }

    private static async Task SeedTenants(ApplicationDbContext context)
    {
        await SeedTenantsInternal(context);
    }

    private static async Task BackfillTeksportInflateFlagAsync(ApplicationDbContext context)
    {
        var tek = await context.Tenants.FirstOrDefaultAsync(t => t.Code == "TEKSPORT");
        if (tek is not null && !tek.InflateImportForWaste)
        {
            tek.InflateImportForWaste = true;
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedTenantsInternal(ApplicationDbContext context)
    {
        // TEKSPORT is a multi-site tenant (Skopje + Vinica). The Warehouses
        // representing each physical site will be linked to this tenant via
        // TenantId once Phase 1.2 adds the column.
        var tenants = new[]
        {
            new Tenant
            {
                Id = Guid.NewGuid(),
                Code = "TEKSPORT",
                Name = "TEKSPORT",
                LegacyUvoznik = "TEKSPORT",
                Country = "MK",
                Address = "Скопје, Република Северна Македонија",
                DefaultLanguage = "mk",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            }
        };
        await context.Tenants.AddRangeAsync(tenants);
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

    private record WarehouseSeed(string Code, string Name, string Address, LocationSeed[] Locations);
    private record LocationSeed(string Code, string Name, LocationType Type, string? Aisle = null, string? Rack = null);

    private static readonly WarehouseSeed[] SeedWarehouseDefinitions =
    {
        new("WH-MAIN", "Main Warehouse", "Industrial Zone, Skopje", DefaultLocationSet()),
        new("WH-TEK-VN", "TEKSPORT Vinica", "Vinica, North Macedonia", DefaultLocationSet()),
    };

    private static LocationSeed[] DefaultLocationSet() => new[]
    {
        new LocationSeed("RCV-01", "Receiving Zone 1", LocationType.Receiving),
        new LocationSeed("STG-A-01", "Storage Aisle A Rack 01", LocationType.Storage, "A", "01"),
        new LocationSeed("STG-A-02", "Storage Aisle A Rack 02", LocationType.Storage, "A", "02"),
        new LocationSeed("PICK-01", "Picking Zone 1", LocationType.Picking),
        new LocationSeed("PROD-01", "Production Floor", LocationType.Production),
        new LocationSeed("SHIP-01", "Shipping Zone", LocationType.Shipping),
        new LocationSeed("QUA-01", "Quarantine", LocationType.Quarantine),
    };

    private static async Task SeedWarehousesIdempotent(ApplicationDbContext context)
    {
        var now = DateTime.UtcNow;

        foreach (var def in SeedWarehouseDefinitions)
        {
            var warehouse = await context.Warehouses.FirstOrDefaultAsync(w => w.Code == def.Code);
            if (warehouse is null)
            {
                warehouse = new Warehouse
                {
                    Id = Guid.NewGuid(),
                    Code = def.Code,
                    Name = def.Name,
                    Address = def.Address,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = "Seed"
                };
                await context.Warehouses.AddAsync(warehouse);
                await context.SaveChangesAsync();
            }

            var existingCodes = await context.Locations
                .Where(l => l.WarehouseId == warehouse.Id)
                .Select(l => l.Code)
                .ToListAsync();

            var missing = def.Locations
                .Where(l => !existingCodes.Contains(l.Code))
                .Select(l => new Location
                {
                    Id = Guid.NewGuid(),
                    Code = l.Code,
                    Name = l.Name,
                    WarehouseId = warehouse.Id,
                    Type = l.Type,
                    Aisle = l.Aisle,
                    Rack = l.Rack,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = "Seed"
                })
                .ToList();

            if (missing.Count > 0)
            {
                await context.Locations.AddRangeAsync(missing);
                await context.SaveChangesAsync();
            }
        }
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
                // Box 37 procedure code. `42` = release for free circulation with
                // simultaneous entry for inward processing (suspension system),
                // `00` = no previous customs procedure.
                Code = "4200",
                Name = "Увоз за облагородување (42 00)",
                Type = CustomsProcedureType.InwardProcessing,
                Description = "Inward processing — suspension system (MK Правилник, член 349)",
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

    /// <summary>
    /// Seeds a baseline LON authorization for TEKSPORT so IM 4200 flows have
    /// a valid `LONAuthorizationId` from day one. Idempotent: looks up by
    /// AuthorizationNumber before inserting.
    /// </summary>
    private static async Task SeedTeksportLONAuthorizationIdempotent(ApplicationDbContext context)
    {
        const string authNumber = "26/TEKSPORT/0001";

        var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Code == "TEKSPORT");
        if (tenant is null) return;

        var existing = await context.LONAuthorizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.AuthorizationNumber == authNumber);

        // Pick any supplier partner; if none, skip (partners seeded first above).
        var partner = await context.Partners
            .Where(p => p.TenantId == tenant.Id && p.Type == PartnerType.Supplier)
            .OrderBy(p => p.Code)
            .FirstOrDefaultAsync();
        if (partner is null) return;

        // If the authorization already exists (prior seed run), we still need
        // to backfill ApprovedItems for B7. Skip only if both exist.
        if (existing is not null)
        {
            var hasItems = await context.LONAuthorizationItems
                .IgnoreQueryFilters()
                .AnyAsync(ai => ai.LONAuthorizationId == existing.Id);
            if (!hasItems)
            {
                await SeedTeksportApprovedItemsAsync(context, tenant.Id, existing.Id);
            }
            return;
        }

        var auth = new LONAuthorization
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            AuthorizationNumber = authNumber,
            PartnerId = partner.Id,
            IssueDate = DateTime.UtcNow.AddDays(-30),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            AuthorizationType = "Повеќекратно",
            SystemType = "ОдложеноПлаќање",
            OperationType = "Обработка",
            EconomicConditionCode = "10",
            GuaranteeAmount = 100000m,
            GuaranteeReference = "GUA-2024-001",
            CompetentCustomsOffice = "MK007",
            SupervisoryOffice = "MK007",
            CompletionPeriodDays = 180,
            Status = "Active",
            Notes = "Seeded baseline authorization for TEKSPORT IM 4200 flows.",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Seed"
        };

        context.LONAuthorizations.Add(auth);
        await context.SaveChangesAsync();

        await SeedTeksportApprovedItemsAsync(context, tenant.Id, auth.Id);
    }

    /// <summary>
    /// B7 helper: pair the TEKSPORT authorization with two real TARIC codes
    /// present in the KB seed data (the tariffs integration tests + VPS smoke
    /// curls use). Idempotent via LONAuthorizationId lookup.
    /// </summary>
    private static async Task SeedTeksportApprovedItemsAsync(
        ApplicationDbContext context, Guid tenantId, Guid authId)
    {
        // Pick two DISTINCT items so the (authId, importItemId) key in the
        // waste-% lookup stays unambiguous — previously both rows shared the
        // same ImportItemId and the dictionary's last-write-wins hid the 5%
        // entry behind the 10% one.
        var items = await context.Items
            .Where(i => i.TenantId == tenantId)
            .OrderBy(i => i.Code)
            .Take(2)
            .ToListAsync();
        if (items.Count == 0) return;

        context.LONAuthorizationItems.Add(new LONAuthorizationItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LONAuthorizationId = authId,
            ImportItemId = items[0].Id,
            ImportTariffCode = "2905399500",
            CompensatingTariffCode = string.Empty,
            YieldRate = 0.95m,
            AllowedWastePercentage = 5m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Seed"
        });

        if (items.Count > 1)
        {
            context.LONAuthorizationItems.Add(new LONAuthorizationItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LONAuthorizationId = authId,
                ImportItemId = items[1].Id,
                ImportTariffCode = "1211200050",
                CompensatingTariffCode = string.Empty,
                YieldRate = 0.90m,
                AllowedWastePercentage = 10m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Seed"
            });
        }

        await context.SaveChangesAsync();
    }
}
