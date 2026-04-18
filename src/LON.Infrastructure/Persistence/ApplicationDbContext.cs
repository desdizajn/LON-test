using System.Reflection;
using LON.Application.Common.Interfaces;
using LON.Domain.Common;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.Production;
using LON.Domain.Entities.Traceability;
using LON.Domain.Entities.WMS;
using Microsoft.EntityFrameworkCore;

namespace LON.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    // Tenants (multi-tenant root)
    public DbSet<Tenant> Tenants => Set<Tenant>();

    // Master Data
    public DbSet<Item> Items => Set<Item>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<ItemUoMConversion> ItemUoMConversions => Set<ItemUoMConversion>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<WorkCenter> WorkCenters => Set<WorkCenter>();
    public DbSet<Machine> Machines => Set<Machine>();
    
    // Knowledge Base - Master Data
    public DbSet<TariffCode> TariffCodes => Set<TariffCode>();
    public DbSet<CustomsRegulation> CustomsRegulations => Set<CustomsRegulation>();
    public DbSet<DeclarationRule> DeclarationRules => Set<DeclarationRule>();
    public DbSet<CodeListItem> CodeListItems => Set<CodeListItem>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeDocumentChunk> KnowledgeDocumentChunks => Set<KnowledgeDocumentChunk>();

    // WMS
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptLine> ReceiptLines => Set<ReceiptLine>();
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<TransferLine> TransferLines => Set<TransferLine>();
    public DbSet<CycleCount> CycleCounts => Set<CycleCount>();
    public DbSet<CycleCountLine> CycleCountLines => Set<CycleCountLine>();
    public DbSet<PickingWave> PickingWaves => Set<PickingWave>();
    public DbSet<PickTask> PickTasks => Set<PickTask>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentLine> ShipmentLines => Set<ShipmentLine>();

    // Production
    public DbSet<BOM> BOMs => Set<BOM>();
    public DbSet<BOMLine> BOMLines => Set<BOMLine>();
    public DbSet<Routing> Routings => Set<Routing>();
    public DbSet<RoutingOperation> RoutingOperations => Set<RoutingOperation>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ProductionOrderMaterial> ProductionOrderMaterials => Set<ProductionOrderMaterial>();
    public DbSet<ProductionOrderOperation> ProductionOrderOperations => Set<ProductionOrderOperation>();
    public DbSet<MaterialIssue> MaterialIssues => Set<MaterialIssue>();
    public DbSet<ProductionReceipt> ProductionReceipts => Set<ProductionReceipt>();

    // Customs
    public DbSet<CustomsProcedure> CustomsProcedures => Set<CustomsProcedure>();
    public DbSet<CustomsProcedureDocument> CustomsProcedureDocuments => Set<CustomsProcedureDocument>();
    public DbSet<CustomsDeclaration> CustomsDeclarations => Set<CustomsDeclaration>();
    public DbSet<CustomsDeclarationLine> CustomsDeclarationLines => Set<CustomsDeclarationLine>();
    public DbSet<CustomsDocument> CustomsDocuments => Set<CustomsDocument>();
    public DbSet<MRNRegistry> MRNRegistries => Set<MRNRegistry>();
    
    // LON (Inward Processing)
    public DbSet<LONAuthorization> LONAuthorizations => Set<LONAuthorization>();
    public DbSet<LONAuthorizationItem> LONAuthorizationItems => Set<LONAuthorizationItem>();

    // Guarantee
    public DbSet<GuaranteeAccount> GuaranteeAccounts => Set<GuaranteeAccount>();
    public DbSet<GuaranteeLedgerEntry> GuaranteeLedgerEntries => Set<GuaranteeLedgerEntry>();
    public DbSet<DutyCalculation> DutyCalculations => Set<DutyCalculation>();

    // Traceability
    public DbSet<TraceLink> TraceLinks => Set<TraceLink>();
    public DbSet<BatchGenealogy> BatchGenealogies => Set<BatchGenealogy>();

    // Outbox
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    private readonly ICurrentUserService? _currentUser;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUser) : base(options)
    {
        _currentUser = currentUser;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Auto-wire Tenant FK + index for every ITenantScoped entity.
        // Uses reflection-dispatched generic helper because the non-generic
        // EntityTypeBuilder overload cannot target an entity type by Type alone
        // when no navigation property exists on the dependent side.
        var configureMethod = typeof(ApplicationDbContext).GetMethod(
            nameof(ConfigureTenantScoped),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(Domain.Common.ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                configureMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(null, new object[] { modelBuilder });
            }
        }
    }

    private static void ConfigureTenantScoped<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, Domain.Common.ITenantScoped
    {
        modelBuilder.Entity<TEntity>()
            .HasOne<Domain.Entities.MasterData.Tenant>()
            .WithMany()
            .HasForeignKey(nameof(Domain.Common.ITenantScoped.TenantId))
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TEntity>()
            .HasIndex(nameof(Domain.Common.ITenantScoped.TenantId));
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditName = _currentUser?.AuditName ?? "System";
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.CreatedBy = auditName;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedAt = DateTime.UtcNow;
                entry.Entity.ModifiedBy = auditName;
            }
        }

        // Auto-fill TenantId on any ITenantScoped entity being Added without one.
        // Inlined here (instead of using ICurrentTenantService) to avoid a DI
        // cycle: ICurrentTenantService needs DbContext, DbContext would need it.
        // Resolution order:
        //   1. Current user's TenantId (via ICurrentUserService + Users lookup)
        //   2. First active Tenant in DB (seeders, background jobs, migrations)
        var scopedEntries = ChangeTracker.Entries<Domain.Common.ITenantScoped>()
            .Where(e => e.State == EntityState.Added && e.Entity.TenantId == Guid.Empty)
            .ToList();
        if (scopedEntries.Count > 0)
        {
            Guid? tenantId = null;
            var userId = _currentUser?.UserId;
            if (userId.HasValue)
            {
                tenantId = await Users
                    .Where(u => u.Id == userId.Value)
                    .Select(u => (Guid?)u.TenantId)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            if (tenantId is null || tenantId == Guid.Empty)
            {
                tenantId = await Tenants
                    .Where(t => t.IsActive)
                    .Select(t => (Guid?)t.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            if (tenantId is not null && tenantId != Guid.Empty)
            {
                foreach (var e in scopedEntries)
                    e.Entity.TenantId = tenantId.Value;
            }
        }

        // Process domain events and create outbox messages
        var domainEntities = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        domainEntities.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = domainEvent.GetType().Name,
                Content = System.Text.Json.JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                OccurredOnUtc = domainEvent.OccurredOn,
                ProcessedOnUtc = null,
                Error = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            OutboxMessages.Add(outboxMessage);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

public class OutboxMessage : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime OccurredOnUtc { get; set; }
    public DateTime? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
}
