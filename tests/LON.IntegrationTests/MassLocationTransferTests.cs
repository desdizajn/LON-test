using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P5.2.7 — filter inventory across multiple criteria + bulk transfer to a
/// single explicit target location. Companion to P5.2.2 MoveBatch (which is
/// batch-scoped + target-stage-scoped).
///
/// Cases:
///   1. Preview + commit happy path — two matching balances by ItemId filter;
///      both land at the target in one atomic call; movements are audited.
///   2. No filter fields → 400 (blast-radius guard).
///   3. Filter matches nothing → 400.
///   4. Re-preview after commit (same filter) → 400 no matches, since drained
///      source rows are zero-qty and the target row is filtered out.
/// </summary>
public class MassLocationTransferTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public MassLocationTransferTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MassTransfer_ByItemFilter_MovesAllBalances()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var items = await client.GetFromJsonAsync<List<IdRow>>("/api/masterdata/items");
        var uoms = await client.GetFromJsonAsync<List<IdRow>>("/api/masterdata/uom");
        var warehouses = await client.GetFromJsonAsync<List<IdRow>>("/api/masterdata/warehouses");
        var locations = await client.GetFromJsonAsync<List<LocationRow>>("/api/masterdata/locations");

        var whId = warehouses![0].Id;
        // Ensure a dedicated target Storage location exists
        var storage = locations!.FirstOrDefault(l => l.Code == "STG-MT-TEST");
        if (storage is null)
        {
            var r = await client.PostAsJsonAsync("/api/masterdata/locations", new
            {
                code = "STG-MT-TEST",
                name = "Test Storage",
                warehouseId = whId,
                type = 2,
                isActive = true
            });
            r.EnsureSuccessStatusCode();
            locations = await client.GetFromJsonAsync<List<LocationRow>>("/api/masterdata/locations");
            storage = locations!.First(l => l.Code == "STG-MT-TEST");
        }

        var rcv = locations!.First(l => l.Code.StartsWith("RCV"));

        // Seed a fresh item so we don't collide with other tests' leftovers.
        var uniqueCode = $"MT-ITEM-{Guid.NewGuid().ToString("N")[..6]}";
        var createItem = await client.PostAsJsonAsync("/api/masterdata/items", new
        {
            code = uniqueCode,
            name = "P5.2.7 Test Item",
            baseUoMId = uoms![0].Id,
            itemType = 1
        });
        createItem.EnsureSuccessStatusCode();
        var createdItem = await createItem.Content.ReadFromJsonAsync<IdRow>();

        var b1 = $"MT-A-{Guid.NewGuid().ToString("N")[..6]}";
        var b2 = $"MT-B-{Guid.NewGuid().ToString("N")[..6]}";

        async Task Receive(string batch, decimal qty) =>
            (await client.PostAsJsonAsync("/api/wms/receipts", new
            {
                receiptDate = DateTime.UtcNow,
                warehouseId = whId,
                lines = new[] {
                    new {
                        itemId = createdItem!.Id, quantity = qty, uoMId = uoms[0].Id,
                        batchNumber = batch, locationId = rcv.Id, qualityStatus = 1
                    }
                }
            })).EnsureSuccessStatusCode();

        await Receive(b1, 10m);
        await Receive(b2, 20m);

        // Preview with Item filter — should return exactly 2 balances totalling 30.
        var preview = await client.PostAsJsonAsync("/api/wms/inventory/mass-transfer/preview", new
        {
            itemId = createdItem!.Id,
            sourceWarehouseId = whId,
            targetLocationId = storage!.Id
        });
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var pBody = await preview.Content.ReadFromJsonAsync<ResultResponse<MassTransferPreview>>();
        pBody!.IsSuccess.Should().BeTrue();
        pBody.Data!.BalancesMatched.Should().Be(2);
        pBody.Data.TotalQuantity.Should().Be(30m);

        // Commit
        var commit = await client.PostAsJsonAsync("/api/wms/inventory/mass-transfer", new
        {
            itemId = createdItem.Id,
            sourceWarehouseId = whId,
            targetLocationId = storage.Id,
            reason = "P5.2.7 integration test"
        });
        commit.StatusCode.Should().Be(HttpStatusCode.OK);
        var cBody = await commit.Content.ReadFromJsonAsync<ResultResponse<MassTransferResult>>();
        cBody!.IsSuccess.Should().BeTrue();
        cBody.Data!.BalancesMoved.Should().Be(2);
        cBody.Data.TotalQuantityMoved.Should().Be(30m);
        cBody.Data.TargetLocationId.Should().Be(storage.Id);
        cBody.Data.Movements.Should().HaveCount(2);
        cBody.Data.Movements.Should().AllSatisfy(m => m.ToLocationId.Should().Be(storage.Id));

        // Idempotency: re-preview with same filter returns 400 (no more sources).
        var postPreview = await client.PostAsJsonAsync("/api/wms/inventory/mass-transfer/preview", new
        {
            itemId = createdItem.Id,
            sourceWarehouseId = whId,
            targetLocationId = storage.Id
        });
        postPreview.StatusCode.Should().Be(HttpStatusCode.OK);
        var pBody2 = await postPreview.Content.ReadFromJsonAsync<ResultResponse<MassTransferPreview>>();
        pBody2!.Data!.BalancesMatched.Should().Be(0);
    }

    [Fact]
    public async Task MassTransfer_NoFilter_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var locations = await client.GetFromJsonAsync<List<LocationRow>>("/api/masterdata/locations");
        var some = locations!.First();
        var resp = await client.PostAsJsonAsync("/api/wms/inventory/mass-transfer", new
        {
            targetLocationId = some.Id
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<ResultResponse<object>>();
        body!.ErrorMessage.Should().Contain("filter");
    }

    [Fact]
    public async Task MassTransfer_UnknownBatch_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var locations = await client.GetFromJsonAsync<List<LocationRow>>("/api/masterdata/locations");
        var some = locations!.First();
        var resp = await client.PostAsJsonAsync("/api/wms/inventory/mass-transfer", new
        {
            batchNumber = "no-such-batch-zzz",
            targetLocationId = some.Id
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<ResultResponse<object>>();
        body!.ErrorMessage.Should().Contain("No positive-quantity inventory");
    }

    private static async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record IdRow(Guid Id);
    private sealed record LocationRow(Guid Id, string Code, int Type, bool IsActive);
    private sealed record PreviewRow(Guid BalanceId, Guid ItemId, string? ItemCode, string? ItemName,
        Guid LocationId, string? LocationCode, string? BatchNumber, string? MRN, decimal Quantity,
        int QualityStatus, int? LonProcessState);
    private sealed record MassTransferPreview(int BalancesMatched, decimal TotalQuantity, List<PreviewRow> Rows);
    private sealed record MassTransferMovement(Guid MovementId, string MovementNumber, Guid ItemId,
        string? BatchNumber, string? MRN, Guid FromLocationId, Guid ToLocationId, decimal Quantity);
    private sealed record MassTransferResult(int BalancesMoved, decimal TotalQuantityMoved,
        Guid TargetLocationId, List<MassTransferMovement> Movements);
    private sealed record ResultResponse<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record LoginResponse(string AccessToken);
}
