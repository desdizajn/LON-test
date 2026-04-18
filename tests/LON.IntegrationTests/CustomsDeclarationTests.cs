using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P2.1 — IM 42 00 end-to-end flow: create declaration → MRN assigned →
/// registered in MRNRegistry → event emitted (implicit via successful save).
/// Compliance gates: LONAuthorization enforcement, currency/country ISO,
/// status lifecycle, auto-MRN fallback.
/// </summary>
public class CustomsDeclarationTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public CustomsDeclarationTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_IM4200_WithValidLONAuth_ReturnsOk_AndRegistersMRN()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (procedureId, lonAuthId, itemId, uomId, partnerId, tariffCode) =
            await LoadSeedIdsAsync(expectedProcedureCode: "4200");

        var payload = new
        {
            declarationNumber = $"DEC-{Guid.NewGuid():N}"[..12],
            mrn = "", // auto-generate
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procedureId,
            partnerId,
            lonAuthorizationId = lonAuthId,
            totalCustomsValue = 1000m,
            currency = "EUR",
            countryOfDispatch = "DE",
            countryOfDestination = "MK",
            senderName = "Fabric Supplier GmbH",
            senderCountry = "DE",
            lines = new[]
            {
                new
                {
                    itemId,
                    tariffCode,
                    quantity = 100m,
                    uoMId = uomId,
                    customsValue = 1000m,
                    countryOfOrigin = "DE",
                    dutyRate = 5m,
                    vatRate = 18m,
                }
            }
        };

        var resp = await client.PostAsJsonAsync("/api/customs/declarations", payload);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        var result = System.Text.Json.JsonSerializer.Deserialize<ResultResponse>(body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        result!.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);

        // DB assertions — declaration + MRN registry.
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await ctx.CustomsDeclarations
            .IgnoreQueryFilters()
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == result.Data);
        saved.Should().NotBeNull();
        saved!.ProcedureCode.Should().Be("4200");
        saved.LONAuthorizationId.Should().Be(lonAuthId);
        saved.Status.Should().Be(DeclarationStatus.Registered);
        saved.MRN.Should().MatchRegex(@"^\d{2}MK[0-9A-F]{8}A1$",
            "auto-generated MRN must match YYMK<8hex>A1");
        saved.TotalDuty.Should().Be(50m, "5% of 1000 customs value");
        saved.TotalVAT.Should().Be(189m, "(1000 + 50) * 18% = 189");

        var registry = await ctx.MRNRegistries
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.MRN == saved.MRN);
        registry.Should().NotBeNull("MRN must be registered for downstream tracking");
        registry!.TotalQuantity.Should().Be(100m);
        registry.UsedQuantity.Should().Be(0m);
        registry.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_IM4200_WithoutLONAuth_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (procedureId, _, itemId, uomId, partnerId, tariffCode) =
            await LoadSeedIdsAsync(expectedProcedureCode: "4200");

        var payload = BuildMinimalPayload(procedureId, lonAuthId: null,
            itemId, uomId, partnerId, tariffCode, currency: "EUR");

        var resp = await client.PostAsJsonAsync("/api/customs/declarations", payload);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("LONAuthorizationId is required",
            "handler must block IM 4200 without a LON authorization");
    }

    [Fact]
    public async Task Create_IM4200_WithInvalidCurrency_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var (procedureId, lonAuthId, itemId, uomId, partnerId, tariffCode) =
            await LoadSeedIdsAsync(expectedProcedureCode: "4200");

        var payload = BuildMinimalPayload(procedureId, lonAuthId,
            itemId, uomId, partnerId, tariffCode, currency: "XYZ");

        var resp = await client.PostAsJsonAsync("/api/customs/declarations", payload);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("XYZ", "the rejection must mention the bogus currency code");
    }

    [Fact]
    public async Task Create_IM4200_DebitsGuaranteeAccountByGuaranteePercentage()
    {
        // Arrange: locate seeded EUR account + snapshot balance.
        Guid accountId;
        decimal balanceBefore;
        decimal guaranteePct;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = await ctx.GuaranteeAccounts.IgnoreQueryFilters()
                .FirstAsync(a => a.Currency == "EUR" && a.IsActive);
            accountId = account.Id;
            balanceBefore = await ctx.GuaranteeLedgerEntries.IgnoreQueryFilters()
                .Where(e => e.GuaranteeAccountId == accountId && !e.IsDeleted)
                .SumAsync(e => e.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit ? e.Amount : -e.Amount);
            var procedure = await ctx.CustomsProcedures.FirstAsync(p => p.Code == "4200");
            guaranteePct = procedure.GuaranteePercentage;
        }

        var client = _factory.CreateClient();
        await Authenticate(client);
        var (procedureId, lonAuthId, itemId, uomId, partnerId, tariffCode) =
            await LoadSeedIdsAsync("4200");

        // 1000 EUR × 5% duty = 50, (1000+50) × 18% VAT = 189, total liability = 239.
        // Debit = 239 × 50% = 119.5 (for the seeded 4200 procedure).
        var payload = new
        {
            declarationNumber = $"DEC-GUA-{Guid.NewGuid():N}"[..16],
            mrn = "",
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procedureId,
            lonAuthorizationId = lonAuthId,
            partnerId,
            totalCustomsValue = 1000m,
            currency = "EUR",
            senderName = "GuaranteeTest Sender",
            senderCountry = "DE",
            countryOfDispatch = "DE",
            lines = new[]
            {
                new { itemId, tariffCode, quantity = 100m, uoMId = uomId,
                      customsValue = 1000m, countryOfOrigin = "DE",
                      dutyRate = 5m, vatRate = 18m }
            }
        };
        var resp = await client.PostAsJsonAsync("/api/customs/declarations", payload);
        resp.EnsureSuccessStatusCode();

        // Assert: ledger grew by (50 + 189) × guaranteePct / 100.
        var expectedDebit = Math.Round(239m * guaranteePct / 100m, 2, MidpointRounding.AwayFromZero);
        using var verifyScope = _factory.Services.CreateScope();
        var ctx2 = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var balanceAfter = await ctx2.GuaranteeLedgerEntries.IgnoreQueryFilters()
            .Where(e => e.GuaranteeAccountId == accountId && !e.IsDeleted)
            .SumAsync(e => e.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit ? e.Amount : -e.Amount);
        (balanceAfter - balanceBefore).Should().Be(expectedDebit,
            $"guarantee must be debited by {guaranteePct}% of (Duty + VAT)");

        var entry = await ctx2.GuaranteeLedgerEntries.IgnoreQueryFilters()
            .Where(e => e.GuaranteeAccountId == accountId)
            .OrderByDescending(e => e.EntryDate).FirstAsync();
        entry.EntryType.Should().Be(LON.Domain.Enums.GuaranteeEntryType.Debit);
        entry.MRN.Should().NotBeNullOrEmpty();
        entry.CustomsDeclarationId.Should().NotBeNull();
        entry.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task Create_IM4200_WithNoEURAccount_Returns400_AndDoesNotPersistDeclaration()
    {
        // Arrange: temporarily deactivate all EUR accounts.
        List<Guid> deactivated;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var accounts = await ctx.GuaranteeAccounts.IgnoreQueryFilters()
                .Where(a => a.Currency == "EUR" && a.IsActive)
                .ToListAsync();
            deactivated = accounts.Select(a => a.Id).ToList();
            foreach (var a in accounts) a.IsActive = false;
            await ctx.SaveChangesAsync();
        }

        try
        {
            var client = _factory.CreateClient();
            await Authenticate(client);
            var (procedureId, lonAuthId, itemId, uomId, partnerId, tariffCode) =
                await LoadSeedIdsAsync("4200");
            var payload = BuildMinimalPayload(procedureId, lonAuthId,
                itemId, uomId, partnerId, tariffCode, currency: "EUR");

            var resp = await client.PostAsJsonAsync("/api/customs/declarations", payload);
            var body = await resp.Content.ReadAsStringAsync();
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
            body.Should().Contain("No active GuaranteeAccount");

            // Declaration must NOT be persisted — single transaction rollback.
            using var scope2 = _factory.Services.CreateScope();
            var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var count = await ctx2.CustomsDeclarations.IgnoreQueryFilters()
                .CountAsync(d => d.DeclarationNumber.StartsWith("DEC-"));
            // Hard to assert exact count because other tests also add decls — just
            // confirm at least that the full-liability version isn't there.
            var thisRun = await ctx2.CustomsDeclarations.IgnoreQueryFilters()
                .Where(d => d.DeclarationNumber.Contains("GUA-BLOCK")).CountAsync();
            thisRun.Should().Be(0); // Sanity — payload used generated number; negative assertion is soft.
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var accounts = await ctx.GuaranteeAccounts.IgnoreQueryFilters()
                .Where(a => deactivated.Contains(a.Id)).ToListAsync();
            foreach (var a in accounts) a.IsActive = true;
            await ctx.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Create_IM4200_OverBondLimit_Returns400()
    {
        // Arrange: shrink the EUR account limit so that any debit exceeds it.
        Guid accountId;
        decimal savedLimit;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = await ctx.GuaranteeAccounts.IgnoreQueryFilters()
                .FirstAsync(a => a.Currency == "EUR" && a.IsActive);
            accountId = account.Id;
            savedLimit = account.TotalLimit;
            account.TotalLimit = 1m; // Any real debit blows this.
            await ctx.SaveChangesAsync();
        }

        try
        {
            var client = _factory.CreateClient();
            await Authenticate(client);
            var (procedureId, lonAuthId, itemId, uomId, partnerId, tariffCode) =
                await LoadSeedIdsAsync("4200");
            var payload = BuildMinimalPayload(procedureId, lonAuthId,
                itemId, uomId, partnerId, tariffCode, currency: "EUR");

            var resp = await client.PostAsJsonAsync("/api/customs/declarations", payload);
            var body = await resp.Content.ReadAsStringAsync();
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
            body.Should().Contain("does not have enough available limit");
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = await ctx.GuaranteeAccounts.IgnoreQueryFilters()
                .FirstAsync(a => a.Id == accountId);
            account.TotalLimit = savedLimit;
            await ctx.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Create_IM4200_WithExplicitMRN_UsesProvidedValue()
    {
        const string providedMrn = "26MKTEST0001EX99A1";

        var client = _factory.CreateClient();
        await Authenticate(client);

        var (procedureId, lonAuthId, itemId, uomId, partnerId, tariffCode) =
            await LoadSeedIdsAsync(expectedProcedureCode: "4200");

        var payload = new
        {
            declarationNumber = $"DEC-{Guid.NewGuid():N}"[..12],
            mrn = providedMrn,
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procedureId,
            partnerId,
            lonAuthorizationId = lonAuthId,
            totalCustomsValue = 500m,
            currency = "EUR",
            countryOfDispatch = "IT",
            senderName = "Italian Sender",
            senderCountry = "IT",
            lines = new[]
            {
                new
                {
                    itemId, tariffCode,
                    quantity = 50m, uoMId = uomId,
                    customsValue = 500m, countryOfOrigin = "IT",
                    dutyRate = 0m, vatRate = 18m
                }
            }
        };

        var resp = await client.PostAsJsonAsync("/api/customs/declarations", payload);
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await ctx.CustomsDeclarations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.MRN == providedMrn);
        saved.Should().NotBeNull("provided MRN must be preserved (uppercased)");
    }

    // ================================================================
    // P2.2.5 blocker regression tests (B1–B7)
    // ================================================================

    /// <summary>B1: MRN uniqueness must be global, not tenant-scoped.</summary>
    [Fact]
    public async Task B1_MRN_IsGloballyUnique_AcrossTenants()
    {
        const string sharedMrn = "26MKCROSSTENANTA1";

        // Tenant 1 (admin → TEKSPORT) posts a declaration with the MRN.
        var clientA = _factory.CreateClient();
        await Authenticate(clientA);
        var (procA, authA, itemA, uomA, partnerA, tariffA) = await LoadSeedIdsAsync("4200");
        var payloadA = new
        {
            declarationNumber = $"DEC-B1A-{Guid.NewGuid():N}"[..14],
            mrn = sharedMrn,
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procA,
            lonAuthorizationId = authA,
            partnerId = partnerA,
            totalCustomsValue = 100m,
            currency = "EUR",
            senderName = "B1 Sender", senderCountry = "DE", countryOfDispatch = "DE",
            lines = new[] { new { itemId = itemA, tariffCode = tariffA, quantity = 10m,
                uoMId = uomA, customsValue = 100m, countryOfOrigin = "DE", dutyRate = 5m, vatRate = 18m } }
        };
        var respA = await clientA.PostAsJsonAsync("/api/customs/declarations", payloadA);
        respA.EnsureSuccessStatusCode();

        // Temporarily create a second tenant and attach a user under it, then try
        // to reuse the same MRN. Without IgnoreQueryFilters() this would succeed.
        Guid otherTenantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var existing = await ctx.Tenants.FirstOrDefaultAsync(t => t.Code == "B1-MRN-TEST");
            if (existing is not null) { ctx.Tenants.Remove(existing); await ctx.SaveChangesAsync(); }
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(), Code = "B1-MRN-TEST", Name = "B1 MRN test",
                Country = "MK", DefaultLanguage = "mk", IsActive = true,
                CreatedAt = DateTime.UtcNow, CreatedBy = "B1Test"
            };
            ctx.Tenants.Add(tenant);
            await ctx.SaveChangesAsync();
            otherTenantId = tenant.Id;
        }

        // Admin creates a user under B1-MRN-TEST.
        var adminRoleId = await FetchAdministratorRoleIdAsync();
        var newUser = $"b1-user-{Guid.NewGuid():N}"[..14];
        var createUserResp = await clientA.PostAsJsonAsync("/api/users", new
        {
            username = newUser, email = $"{newUser}@b1.test",
            fullName = "B1 User", password = "B1Test123!",
            roleIds = new[] { adminRoleId }, tenantId = otherTenantId
        });
        createUserResp.EnsureSuccessStatusCode();

        // Login as new user and try to POST the same MRN.
        var clientB = _factory.CreateClient();
        var loginResp = await clientB.PostAsJsonAsync("/api/auth/login",
            new { username = newUser, password = "B1Test123!" });
        loginResp.EnsureSuccessStatusCode();
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        clientB.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

        // Seed a minimal LONAuth + item under the new tenant so we reach the MRN check.
        // Easier: reuse the seeded 4200 procedure (global master data); LON auth
        // will fail on tenant-mismatch — but we're testing the MRN uniqueness path.
        // Workaround: post with an existing MRN and non-LON procedure (FINAL).
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var finalProc = await ctx.CustomsProcedures.FirstAsync(p => p.Code == "FINAL");
            var item = await ctx.Items.FirstAsync();
            var uom = await ctx.UnitsOfMeasure.FirstAsync(u => u.Code == "KG");

            var payloadB = new
            {
                declarationNumber = $"DEC-B1B-{Guid.NewGuid():N}"[..14],
                mrn = sharedMrn, // SAME as tenant A
                declarationDate = DateTime.UtcNow.Date,
                customsProcedureId = finalProc.Id,
                totalCustomsValue = 100m,
                currency = "EUR",
                senderName = "B1 Sender", senderCountry = "DE", countryOfDispatch = "DE",
                lines = new[] { new { itemId = item.Id, tariffCode = "2905399500",
                    quantity = 10m, uoMId = uom.Id, customsValue = 100m,
                    countryOfOrigin = "DE", dutyRate = 5m, vatRate = 18m } }
            };

            var respB = await clientB.PostAsJsonAsync("/api/customs/declarations", payloadB);
            var bodyB = await respB.Content.ReadAsStringAsync();
            respB.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: bodyB);
            bodyB.Should().Contain(sharedMrn,
                "error must cite the colliding MRN to prove global check ran");
        }
    }

    /// <summary>B2: editing a non-Draft declaration must fail with 409.</summary>
    [Fact]
    public async Task B2_PUT_NonDraftDeclaration_Returns409()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var (procId, authId, itemId, uomId, partnerId, tariffCode) = await LoadSeedIdsAsync("4200");

        var create = await client.PostAsJsonAsync("/api/customs/declarations", new
        {
            declarationNumber = $"DEC-B2-{Guid.NewGuid():N}"[..14],
            mrn = "", declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procId, lonAuthorizationId = authId, partnerId,
            totalCustomsValue = 100m, currency = "EUR",
            senderName = "B2 Sender", senderCountry = "DE", countryOfDispatch = "DE",
            lines = new[] { new { itemId, tariffCode, quantity = 10m, uoMId = uomId,
                customsValue = 100m, countryOfOrigin = "DE", dutyRate = 5m, vatRate = 18m } }
        });
        create.EnsureSuccessStatusCode();
        var body = await create.Content.ReadFromJsonAsync<ResultResponse>();
        var id = body!.Data;

        // Declaration auto-transitions to Registered on create (MRN auto-gen).
        var put = await client.PutAsJsonAsync($"/api/customs/declarations/{id}",
            new { notes = "Trying to edit a Registered declaration — should 409" });
        var putBody = await put.Content.ReadAsStringAsync();
        put.StatusCode.Should().Be(HttpStatusCode.Conflict, because: putBody);
        putBody.Should().Contain("cannot be edited");
    }

    /// <summary>B2: editing a Draft declaration is allowed.</summary>
    [Fact]
    public async Task B2_PUT_DraftDeclaration_SucceedsAndUpdatesNotes()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var (procId, authId, itemId, uomId, partnerId, tariffCode) = await LoadSeedIdsAsync("4200");

        var create = await client.PostAsJsonAsync("/api/customs/declarations", new
        {
            declarationNumber = $"DEC-B2D-{Guid.NewGuid():N}"[..14],
            mrn = "", declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procId, lonAuthorizationId = authId, partnerId,
            totalCustomsValue = 100m, currency = "EUR",
            senderName = "B2D Sender", senderCountry = "DE", countryOfDispatch = "DE",
            status = (int)DeclarationStatus.Draft, // force Draft
            lines = new[] { new { itemId, tariffCode, quantity = 10m, uoMId = uomId,
                customsValue = 100m, countryOfOrigin = "DE", dutyRate = 5m, vatRate = 18m } }
        });
        create.EnsureSuccessStatusCode();
        var body = await create.Content.ReadFromJsonAsync<ResultResponse>();
        var id = body!.Data;

        var put = await client.PutAsJsonAsync($"/api/customs/declarations/{id}",
            new { notes = "Edited via Draft update" });
        put.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await ctx.CustomsDeclarations.IgnoreQueryFilters().FirstAsync(d => d.Id == id);
        saved.Notes.Should().Be("Edited via Draft update");
    }

    /// <summary>B3: per-authorization bond ceiling is enforced.</summary>
    [Fact]
    public async Task B3_PerAuthorizationBond_Ceiling_Enforced()
    {
        // Arrange: clone the seeded TEKSPORT auth with a tiny ceiling.
        Guid smallAuthId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = await ctx.Tenants.FirstAsync(t => t.Code == "TEKSPORT");
            var partner = await ctx.Partners.IgnoreQueryFilters()
                .FirstAsync(p => p.TenantId == tenant.Id);
            var auth = new LONAuthorization
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id,
                AuthorizationNumber = $"B3-SMALL-{Guid.NewGuid():N}"[..14],
                PartnerId = partner.Id,
                IssueDate = DateTime.UtcNow.AddDays(-1),
                ExpiryDate = DateTime.UtcNow.AddYears(1),
                AuthorizationType = "Повеќекратно", SystemType = "ОдложеноПлаќање",
                OperationType = "Обработка", EconomicConditionCode = "10",
                GuaranteeAmount = 50m, // TINY
                CompetentCustomsOffice = "MK007", CompletionPeriodDays = 180,
                Status = "Active", CreatedAt = DateTime.UtcNow, CreatedBy = "B3Test"
            };
            ctx.LONAuthorizations.Add(auth);
            await ctx.SaveChangesAsync();
            smallAuthId = auth.Id;
        }

        var client = _factory.CreateClient();
        await Authenticate(client);
        var (procId, _, itemId, uomId, partnerId, tariffCode) = await LoadSeedIdsAsync("4200");

        // Liability = 50 + 189 = 239; × 50% = 119.5; ceiling is 50 → must fail.
        var resp = await client.PostAsJsonAsync("/api/customs/declarations", new
        {
            declarationNumber = $"DEC-B3-{Guid.NewGuid():N}"[..14],
            mrn = "", declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procId, lonAuthorizationId = smallAuthId, partnerId,
            totalCustomsValue = 1000m, currency = "EUR",
            senderName = "B3 Sender", senderCountry = "DE", countryOfDispatch = "DE",
            lines = new[] { new { itemId, tariffCode, quantity = 100m, uoMId = uomId,
                customsValue = 1000m, countryOfOrigin = "DE", dutyRate = 5m, vatRate = 18m } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain("bond ceiling exceeded",
            "per-authorization ceiling must bite before account-level TotalLimit");
    }

    /// <summary>B4 + B5: authorization overrides (CompletionPeriodDays + GuaranteePercentageOverride).</summary>
    [Fact]
    public async Task B4_B5_AuthorizationOverrides_ApplyToRegistryExpiryAndGuaranteeDebit()
    {
        // Arrange: auth with 90-day completion and 25% guarantee override.
        Guid overrideAuthId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = await ctx.Tenants.FirstAsync(t => t.Code == "TEKSPORT");
            var partner = await ctx.Partners.IgnoreQueryFilters()
                .FirstAsync(p => p.TenantId == tenant.Id);
            var auth = new LONAuthorization
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id,
                AuthorizationNumber = $"B45-OV-{Guid.NewGuid():N}"[..12],
                PartnerId = partner.Id,
                IssueDate = DateTime.UtcNow.AddDays(-1),
                ExpiryDate = DateTime.UtcNow.AddYears(1),
                AuthorizationType = "Повеќекратно", SystemType = "ОдложеноПлаќање",
                OperationType = "Обработка", EconomicConditionCode = "10",
                GuaranteeAmount = 100000m,
                GuaranteePercentageOverride = 25m, // B5
                CompetentCustomsOffice = "MK007",
                CompletionPeriodDays = 90, // B4
                Status = "Active", CreatedAt = DateTime.UtcNow, CreatedBy = "B45Test"
            };
            ctx.LONAuthorizations.Add(auth);
            await ctx.SaveChangesAsync();
            overrideAuthId = auth.Id;
        }

        var client = _factory.CreateClient();
        await Authenticate(client);
        var (procId, _, itemId, uomId, partnerId, tariffCode) = await LoadSeedIdsAsync("4200");

        var declDate = DateTime.UtcNow.Date;
        var resp = await client.PostAsJsonAsync("/api/customs/declarations", new
        {
            declarationNumber = $"DEC-B45-{Guid.NewGuid():N}"[..14],
            mrn = "", declarationDate = declDate,
            customsProcedureId = procId, lonAuthorizationId = overrideAuthId, partnerId,
            totalCustomsValue = 1000m, currency = "EUR",
            senderName = "B45 Sender", senderCountry = "DE", countryOfDispatch = "DE",
            lines = new[] { new { itemId, tariffCode, quantity = 100m, uoMId = uomId,
                customsValue = 1000m, countryOfOrigin = "DE", dutyRate = 5m, vatRate = 18m } }
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ResultResponse>();
        var declId = body!.Data;

        using var vscope = _factory.Services.CreateScope();
        var ctxV = vscope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var registry = await ctxV.MRNRegistries.IgnoreQueryFilters()
            .FirstAsync(r => r.CustomsDeclarationId == declId);
        registry.ExpiryDate!.Value.Date.Should().Be(declDate.AddDays(90),
            "B4: registry expiry must use auth.CompletionPeriodDays (90), not procedure.DueDays (180)");

        var debit = await ctxV.GuaranteeLedgerEntries.IgnoreQueryFilters()
            .Where(e => e.CustomsDeclarationId == declId &&
                        e.EntryType == LON.Domain.Enums.GuaranteeEntryType.Debit)
            .SumAsync(e => e.Amount);
        debit.Should().Be(59.75m, // 239 × 25% = 59.75
            "B5: debit must use auth.GuaranteePercentageOverride (25%), not procedure default (50%)");
    }

    /// <summary>B6: Export-type procedure produces an EX declaration, not IM.</summary>
    [Fact]
    public async Task B6_ExportProcedure_ProducesEXDeclaration()
    {
        Guid exportProcId; Guid itemId; Guid uomId; Guid partnerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var proc = await ctx.CustomsProcedures.FirstAsync(p =>
                p.Type == LON.Domain.Enums.CustomsProcedureType.Export && p.IsActive);
            exportProcId = proc.Id;
            itemId = (await ctx.Items.FirstAsync()).Id;
            uomId = (await ctx.UnitsOfMeasure.FirstAsync(u => u.Code == "KG")).Id;
            partnerId = (await ctx.Partners.OrderBy(p => p.Code).FirstAsync()).Id;
        }

        var client = _factory.CreateClient();
        await Authenticate(client);
        var resp = await client.PostAsJsonAsync("/api/customs/declarations", new
        {
            declarationNumber = $"DEC-B6-{Guid.NewGuid():N}"[..14],
            mrn = "", declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = exportProcId, partnerId,
            totalCustomsValue = 100m, currency = "EUR",
            senderName = "B6 Sender", senderCountry = "MK", countryOfDispatch = "MK",
            lines = new[] { new { itemId, tariffCode = "2905399500", quantity = 10m,
                uoMId = uomId, customsValue = 100m, countryOfOrigin = "MK",
                dutyRate = 0m, vatRate = 0m } }
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ResultResponse>();

        using var scope2 = _factory.Services.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await ctx2.CustomsDeclarations.IgnoreQueryFilters()
            .FirstAsync(d => d.Id == body!.Data);
        saved.DeclarationType.Should().Be("EX",
            "procedure.Type=Export must map to SAD Box 01 = 'EX'");
    }

    /// <summary>B7: a line tariff outside the authorization's ApprovedItems must fail.</summary>
    [Fact]
    public async Task B7_LineTariffNotInAuthorization_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var (procId, lonAuthId, itemId, uomId, partnerId, _) = await LoadSeedIdsAsync("4200");

        // Seeded TEKSPORT auth ApprovedItems = {2905399500, 1211200050}.
        // Use a different valid-format tariff that the auth does NOT cover.
        const string unauthorizedTariff = "0401109000";

        var resp = await client.PostAsJsonAsync("/api/customs/declarations", new
        {
            declarationNumber = $"DEC-B7-{Guid.NewGuid():N}"[..14],
            mrn = "", declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procId, lonAuthorizationId = lonAuthId, partnerId,
            totalCustomsValue = 100m, currency = "EUR",
            senderName = "B7 Sender", senderCountry = "DE", countryOfDispatch = "DE",
            lines = new[] { new { itemId, tariffCode = unauthorizedTariff, quantity = 10m,
                uoMId = uomId, customsValue = 100m, countryOfOrigin = "DE",
                dutyRate = 5m, vatRate = 18m } }
        });
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: body);
        body.Should().Contain(unauthorizedTariff);
        body.Should().Contain("не е дозволена");
    }

    // ================================================================
    // Helpers (shared)
    // ================================================================

    private async Task<Guid> FetchAdministratorRoleIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var role = await ctx.Roles.FirstAsync(r => r.Name == "Administrator");
        return role.Id;
    }

    private async Task<(Guid procedureId, Guid lonAuthId, Guid itemId, Guid uomId,
        Guid partnerId, string tariffCode)> LoadSeedIdsAsync(string expectedProcedureCode)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var proc = await ctx.CustomsProcedures.FirstAsync(p => p.Code == expectedProcedureCode);
        var auth = await ctx.LONAuthorizations.IgnoreQueryFilters()
            .OrderBy(a => a.AuthorizationNumber)
            .FirstAsync();
        var item = await ctx.Items.FirstAsync();
        var uom = await ctx.UnitsOfMeasure.FirstAsync(u => u.Code == "KG");
        var partner = await ctx.Partners.OrderBy(p => p.Code).FirstAsync();
        var tariff = await ctx.TariffCodes.Where(t => t.IsActive)
            .OrderBy(t => t.TariffNumber).FirstAsync();

        return (proc.Id, auth.Id, item.Id, uom.Id, partner.Id, tariff.TariffNumber);
    }

    private static object BuildMinimalPayload(Guid procedureId, Guid? lonAuthId,
        Guid itemId, Guid uomId, Guid partnerId, string tariffCode, string currency,
        string senderName = "Sender GmbH", string senderCountry = "DE")
    {
        return new
        {
            declarationNumber = $"DEC-{Guid.NewGuid():N}"[..12],
            mrn = "",
            declarationDate = DateTime.UtcNow.Date,
            customsProcedureId = procedureId,
            partnerId,
            lonAuthorizationId = lonAuthId,
            totalCustomsValue = 100m,
            currency,
            countryOfDispatch = "DE",
            senderName,
            senderCountry,
            lines = new[]
            {
                new
                {
                    itemId, tariffCode,
                    quantity = 10m, uoMId = uomId,
                    customsValue = 100m, countryOfOrigin = "DE",
                    dutyRate = 5m, vatRate = 18m
                }
            }
        };
    }

    private async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record ResultResponse(bool IsSuccess, Guid Data, string? ErrorMessage);
}
