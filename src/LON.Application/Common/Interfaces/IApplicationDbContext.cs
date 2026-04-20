using LON.Domain.Entities.Audit;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Entities.Importing;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.Production;
using LON.Domain.Entities.Traceability;
using LON.Domain.Entities.WMS;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Tenants (multi-tenant root)
    DbSet<Tenant> Tenants { get; }

    // Master Data
    DbSet<Item> Items { get; }
    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<ItemUoMConversion> ItemUoMConversions { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Location> Locations { get; }
    DbSet<Partner> Partners { get; }
    DbSet<Employee> Employees { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<WorkCenter> WorkCenters { get; }
    DbSet<Machine> Machines { get; }
    DbSet<UserFieldHistory> UserFieldHistories { get; }

    // Knowledge Base
    DbSet<TariffCode> TariffCodes { get; }
    DbSet<TariffCodeRate> TariffCodeRates { get; }
    DbSet<CustomsRegulation> CustomsRegulations { get; }
    DbSet<DeclarationRule> DeclarationRules { get; }
    DbSet<CodeListItem> CodeListItems { get; }
    DbSet<KnowledgeDocument> KnowledgeDocuments { get; }
    DbSet<KnowledgeDocumentChunk> KnowledgeDocumentChunks { get; }

    // WMS
    DbSet<Receipt> Receipts { get; }
    DbSet<ReceiptLine> ReceiptLines { get; }
    DbSet<InventoryBalance> InventoryBalances { get; }
    DbSet<InventoryMovement> InventoryMovements { get; }
    DbSet<Transfer> Transfers { get; }
    DbSet<TransferLine> TransferLines { get; }
    DbSet<CycleCount> CycleCounts { get; }
    DbSet<CycleCountLine> CycleCountLines { get; }
    DbSet<PickingWave> PickingWaves { get; }
    DbSet<PickTask> PickTasks { get; }
    DbSet<Shipment> Shipments { get; }
    DbSet<ShipmentLine> ShipmentLines { get; }

    // Production
    DbSet<BOM> BOMs { get; }
    DbSet<BOMLine> BOMLines { get; }
    DbSet<Routing> Routings { get; }
    DbSet<RoutingOperation> RoutingOperations { get; }
    DbSet<ProductionOrder> ProductionOrders { get; }
    DbSet<ProductionOrderMaterial> ProductionOrderMaterials { get; }
    DbSet<ProductionOrderOperation> ProductionOrderOperations { get; }
    DbSet<MaterialIssue> MaterialIssues { get; }
    DbSet<ProductionReceipt> ProductionReceipts { get; }

    // Customs
    DbSet<CustomsProcedure> CustomsProcedures { get; }
    DbSet<CustomsProcedureDocument> CustomsProcedureDocuments { get; }
    DbSet<CustomsDeclaration> CustomsDeclarations { get; }
    DbSet<CustomsDeclarationLine> CustomsDeclarationLines { get; }
    DbSet<CustomsDocument> CustomsDocuments { get; }
    DbSet<MRNRegistry> MRNRegistries { get; }

    // LON (Inward Processing)
    DbSet<LONAuthorization> LONAuthorizations { get; }
    DbSet<LONAuthorizationItem> LONAuthorizationItems { get; }

    // Guarantee
    DbSet<GuaranteeAccount> GuaranteeAccounts { get; }
    DbSet<GuaranteeLedgerEntry> GuaranteeLedgerEntries { get; }
    DbSet<DutyCalculation> DutyCalculations { get; }

    // Traceability
    DbSet<TraceLink> TraceLinks { get; }
    DbSet<BatchGenealogy> BatchGenealogies { get; }

    // Audit (I8)
    DbSet<AuditLogEntry> AuditLogEntries { get; }

    // Importing (P5.1)
    DbSet<ImportSession> ImportSessions { get; }
    DbSet<ImportMappingProfile> ImportMappingProfiles { get; }

    /// <summary>
    /// Tenant id lifted from the authenticated JWT's `tenant_id` claim. Null
    /// during seeders / background jobs / login flow. Exposed so handlers
    /// that need to bypass the global query filter (e.g. G7 — "undelete soft
    /// deleted row in MY tenant") can still scope by tenant manually.
    /// </summary>
    Guid? CurrentTenantId { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
