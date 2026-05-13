using System.Reflection;
using System.Text.Json;
using LON.Application.Common.Interfaces;
using LON.Domain.Common;
using LON.Domain.Entities.Audit;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.Finance;
using LON.Domain.Entities.Guarantee;
using LON.Domain.Entities.Importing;
using LON.Domain.Entities.Management;
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
    public DbSet<MachineStateEvent> MachineStateEvents => Set<MachineStateEvent>();
    public DbSet<DowntimeEvent> DowntimeEvents => Set<DowntimeEvent>();
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<MaintenanceWorkOrder> MaintenanceWorkOrders => Set<MaintenanceWorkOrder>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Absence> Absences => Set<Absence>();
    public DbSet<OperatorMachineAssignment> OperatorMachineAssignments => Set<OperatorMachineAssignment>();
    public DbSet<UserFieldHistory> UserFieldHistories => Set<UserFieldHistory>();

    // Knowledge Base - Master Data
    public DbSet<TariffCode> TariffCodes => Set<TariffCode>();
    public DbSet<TariffCodeRate> TariffCodeRates => Set<TariffCodeRate>();
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
    public DbSet<Skart> Skarts => Set<Skart>();
    public DbSet<Domain.Entities.Guarantee.GuaranteeBalanceSnapshot> GuaranteeBalanceSnapshots =>
        Set<Domain.Entities.Guarantee.GuaranteeBalanceSnapshot>();

    // Production
    public DbSet<BOM> BOMs => Set<BOM>();
    public DbSet<BOMLine> BOMLines => Set<BOMLine>();
    public DbSet<Routing> Routings => Set<Routing>();
    public DbSet<RoutingOperation> RoutingOperations => Set<RoutingOperation>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ProductionOrderMaterial> ProductionOrderMaterials => Set<ProductionOrderMaterial>();
    public DbSet<ProductionOrderMaterialSize> ProductionOrderMaterialSizes => Set<ProductionOrderMaterialSize>();
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
    public DbSet<ClientOrder> ClientOrders => Set<ClientOrder>();
    public DbSet<ClientOrderFinishedGood> ClientOrderFinishedGoods => Set<ClientOrderFinishedGood>();
    public DbSet<CommercialInvoice> CommercialInvoices => Set<CommercialInvoice>();
    public DbSet<CommercialInvoiceLine> CommercialInvoiceLines => Set<CommercialInvoiceLine>();

    // LON (Inward Processing)
    public DbSet<LONAuthorization> LONAuthorizations => Set<LONAuthorization>();
    public DbSet<LONAuthorizationItem> LONAuthorizationItems => Set<LONAuthorizationItem>();

    // Guarantee
    public DbSet<GuaranteeAccount> GuaranteeAccounts => Set<GuaranteeAccount>();
    public DbSet<GuaranteeLedgerEntry> GuaranteeLedgerEntries => Set<GuaranteeLedgerEntry>();
    public DbSet<DutyCalculation> DutyCalculations => Set<DutyCalculation>();

    // Finance (P12.2 / P12.3)
    public DbSet<ClientContract> ClientContracts => Set<ClientContract>();
    public DbSet<RateCardEntry> RateCardEntries => Set<RateCardEntry>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    // Traceability
    public DbSet<TraceLink> TraceLinks => Set<TraceLink>();
    public DbSet<BatchGenealogy> BatchGenealogies => Set<BatchGenealogy>();

    // Audit (I8)
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    // Importing (P5.1)
    public DbSet<ImportSession> ImportSessions => Set<ImportSession>();
    public DbSet<ImportMappingProfile> ImportMappingProfiles => Set<ImportMappingProfile>();

    // Management (P16.C1)
    public DbSet<RiskRegisterItem> RiskRegisterItems => Set<RiskRegisterItem>();

    // HR (P16.C2)
    public DbSet<EmployeeCertification> EmployeeCertifications => Set<EmployeeCertification>();

    // Finance — P16.C3
    public DbSet<CostRate> CostRates => Set<CostRate>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollLine> PayrollLines => Set<PayrollLine>();
    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();

    // Logistics — Phase 17 §E7.6 (D5)
    public DbSet<LON.Domain.Entities.Logistics.DeliveryNote> DeliveryNotes => Set<LON.Domain.Entities.Logistics.DeliveryNote>();
    public DbSet<LON.Domain.Entities.Logistics.DeliveryNoteLine> DeliveryNoteLines => Set<LON.Domain.Entities.Logistics.DeliveryNoteLine>();

    // AI assistant — Phase 17 §E10
    public DbSet<LON.Domain.Entities.Ai.AiSuggestionLog> AiSuggestionLogs => Set<LON.Domain.Entities.Ai.AiSuggestionLog>();

    // Management alerts — Phase 17 §E10.5
    public DbSet<LON.Domain.Entities.Management.AlertRule> AlertRules => Set<LON.Domain.Entities.Management.AlertRule>();
    public DbSet<LON.Domain.Entities.Management.AlertEvent> AlertEvents => Set<LON.Domain.Entities.Management.AlertEvent>();

    // Outbox
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    private readonly ICurrentUserService? _currentUser;

    /// <summary>
    /// Captured once per DbContext instance from the authenticated JWT's
    /// `tenant_id` claim (P1.3). Used by the ITenantScoped global query
    /// filter (P1.4): when non-null, every query for a scoped entity is
    /// restricted to rows where TenantId matches. When null (seeders,
    /// migrations, login before authentication), the filter is bypassed
    /// so those paths see all rows.
    /// EF re-reads this field for each query (closure over DbContext
    /// instance), so the value is always current for the request in flight.
    /// </summary>
    public Guid? CurrentTenantId { get; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUser) : base(options)
    {
        _currentUser = currentUser;
        CurrentTenantId = currentUser.TenantId;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Auto-wire Tenant FK + index + global query filter for every
        // ITenantScoped entity. Uses reflection-dispatched generic helper
        // because the non-generic EntityTypeBuilder overloads cannot target
        // an entity type by Type alone when no navigation property exists.
        // Instance method: the query filter closes over `this.CurrentTenantId`
        // which EF re-reads per query (per-instance scoping).
        var configureMethod = typeof(ApplicationDbContext).GetMethod(
            nameof(ConfigureTenantScoped),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(Domain.Common.ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                configureMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void ConfigureTenantScoped<TEntity>(ModelBuilder modelBuilder)
        where TEntity : BaseEntity, Domain.Common.ITenantScoped
    {
        modelBuilder.Entity<TEntity>()
            .HasOne<Domain.Entities.MasterData.Tenant>()
            .WithMany()
            .HasForeignKey(nameof(Domain.Common.ITenantScoped.TenantId))
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TEntity>()
            .HasIndex(nameof(Domain.Common.ITenantScoped.TenantId));

        // Combined filter: soft-delete AND tenant scope. This REPLACES the
        // per-entity `HasQueryFilter(e => !e.IsDeleted)` that entity
        // configurations declare — soft delete stays enforced via this
        // combined filter. When CurrentTenantId is null (seeders, migrations,
        // login pre-auth), the tenant clause is bypassed so those paths see
        // every tenant's rows.
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => !e.IsDeleted && (CurrentTenantId == null || e.TenantId == CurrentTenantId));
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
        //   1. `tenant_id` JWT claim via ICurrentUserService (P1.3, zero DB hits)
        //   2. Current user's TenantId (Users row lookup — for pre-P1.3 tokens)
        //   3. First active Tenant in DB (seeders, background jobs, migrations)
        var scopedEntries = ChangeTracker.Entries<Domain.Common.ITenantScoped>()
            .Where(e => e.State == EntityState.Added && e.Entity.TenantId == Guid.Empty)
            .ToList();
        if (scopedEntries.Count > 0)
        {
            Guid? tenantId = _currentUser?.TenantId;
            if (tenantId is null || tenantId == Guid.Empty)
            {
                var userId = _currentUser?.UserId;
                if (userId.HasValue)
                {
                    tenantId = await Users
                        .Where(u => u.Id == userId.Value)
                        .Select(u => (Guid?)u.TenantId)
                        .FirstOrDefaultAsync(cancellationToken);
                }
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

        // I8 audit log — snapshot changes to IAuditable entities so every
        // create/update/soft-delete produces one AuditLogEntry row in the
        // same transaction. `CaptureAuditEntries()` must run AFTER all
        // business mutations but BEFORE base.SaveChangesAsync so we have
        // both the original and current values on tracked entities.
        var auditEntries = CaptureAuditEntries();
        if (auditEntries.Count > 0)
            AuditLogEntries.AddRange(auditEntries);

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Walks the ChangeTracker for <see cref="IAuditable"/> entities in
    /// Added/Modified/Deleted state and produces one <see cref="AuditLogEntry"/>
    /// per change. Tracks field-level diffs for updates; skips audit-log rows
    /// themselves (no recursion).
    /// </summary>
    private List<AuditLogEntry> CaptureAuditEntries()
    {
        var results = new List<AuditLogEntry>();
        var auditTs = DateTime.UtcNow;
        var userId = _currentUser?.UserId;
        var userName = _currentUser?.AuditName ?? "System";

        foreach (var e in ChangeTracker.Entries<IAuditable>())
        {
            if (e.State != EntityState.Added
                && e.State != EntityState.Modified
                && e.State != EntityState.Deleted)
                continue;

            var entityTypeName = e.Entity.GetType().Name;
            var entityId = GetPrimaryKey(e);
            if (entityId == Guid.Empty) continue; // shadow PK not yet assigned — skip

            string action;
            string changesJson;
            switch (e.State)
            {
                case EntityState.Added:
                    action = "Create";
                    changesJson = SerializeInitialValues(e);
                    break;
                case EntityState.Modified:
                    // Soft delete shows as Modified with IsDeleted=true.
                    var isSoftDelete = e.Property(nameof(BaseEntity.IsDeleted)).IsModified
                        && e.Property(nameof(BaseEntity.IsDeleted)).CurrentValue is true;
                    action = isSoftDelete ? "Delete" : "Update";
                    changesJson = SerializeFieldDiffs(e);
                    break;
                case EntityState.Deleted:
                    action = "Delete";
                    changesJson = "[]";
                    break;
                default:
                    continue;
            }

            // Resolve TenantId from the audited entity when possible. The
            // IAuditable entities in scope today (CustomsDeclaration, Receipt,
            // User, LONAuthorization, GuaranteeLedgerEntry) are all
            // ITenantScoped — we pull TenantId directly from the changed row
            // so audit never FK-fails at login (CurrentTenantId is null until
            // the JWT is issued).
            Guid auditTenantId = Guid.Empty;
            if (e.Entity is ITenantScoped scoped && scoped.TenantId != Guid.Empty)
                auditTenantId = scoped.TenantId;
            else if (CurrentTenantId.HasValue && CurrentTenantId.Value != Guid.Empty)
                auditTenantId = CurrentTenantId.Value;
            else
                continue; // cannot attach to any tenant — skip rather than corrupt the log

            results.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                EntityType = entityTypeName,
                EntityId = entityId,
                Action = action,
                ChangesJson = changesJson,
                UserId = userId,
                UserName = userName,
                OccurredAt = auditTs,
                CreatedAt = auditTs,
                CreatedBy = userName,
                TenantId = auditTenantId
            });
        }

        return results;
    }

    private static Guid GetPrimaryKey(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var pk = entry.Metadata.FindPrimaryKey();
        if (pk is null) return Guid.Empty;
        var prop = pk.Properties.FirstOrDefault();
        if (prop is null) return Guid.Empty;
        var val = entry.Property(prop.Name).CurrentValue;
        return val is Guid g ? g : Guid.Empty;
    }

    /// <summary>Returns a JSON array of every scalar property's value — used for Added state.</summary>
    private static string SerializeInitialValues(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var diffs = new List<object>();
        foreach (var prop in entry.Properties)
        {
            if (IsAuditNoise(prop.Metadata.Name)) continue;
            var newValue = prop.CurrentValue;
            if (newValue is null) continue;
            diffs.Add(new { field = prop.Metadata.Name, @new = newValue?.ToString() });
        }
        return JsonSerializer.Serialize(diffs);
    }

    /// <summary>Returns a JSON array of fields whose original ≠ current value.</summary>
    private static string SerializeFieldDiffs(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var diffs = new List<object>();
        foreach (var prop in entry.Properties)
        {
            if (!prop.IsModified) continue;
            if (IsAuditNoise(prop.Metadata.Name)) continue;
            var oldValue = prop.OriginalValue;
            var newValue = prop.CurrentValue;
            if (Equals(oldValue, newValue)) continue;
            diffs.Add(new
            {
                field = prop.Metadata.Name,
                old = oldValue?.ToString(),
                @new = newValue?.ToString()
            });
        }
        return JsonSerializer.Serialize(diffs);
    }

    private static bool IsAuditNoise(string fieldName) => fieldName switch
    {
        nameof(BaseEntity.ModifiedAt) => true,
        nameof(BaseEntity.ModifiedBy) => true,
        nameof(BaseEntity.CreatedAt) => true,
        nameof(BaseEntity.CreatedBy) => true,
        _ => false
    };
}

public class OutboxMessage : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime OccurredOnUtc { get; set; }
    public DateTime? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
}
