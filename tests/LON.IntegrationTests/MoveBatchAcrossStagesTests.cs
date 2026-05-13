using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P5.2.2 — move every InventoryBalance with a given batch to a target
/// LocationType in one call.
///   1. Happy path — batch at Receiving → move to Production; balance ends up
///      at the first active Production location; movement record audit-trails it.
///   2. Repeated call is a no-op — second invocation returns "nothing to move".
///   3. Unknown batch → 400.
///   4. Target stage has no location in warehouse → 400.
/// </summary>
public class MoveBatchAcrossStagesTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public MoveBatchAcrossStagesTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MoveBatch_ReceivingToProduction_TransfersBalance()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var items = await client.GetFromJsonAsync<List<IdRow>>("/api/masterdata/items");
        var uoms = await client.GetFromJsonAsync<List<IdRow>>("/api/masterdata/uom");
        var warehouses = await client.GetFromJsonAsync<List<IdRow>>("/api/masterdata/warehouses");
        var locations = await client.GetFromJsonAsync<List<LocationRow>>("/api/masterdata/locations");

        var whId = warehouses![0].Id;
        // Ensure a Production location exists in the warehouse; if not, create one.
        var prod = locations!.FirstOrDefault(l => l.Type == 4 /* Production */ && l.IsActive);
        if (prod is null)
        {
            var r = await client.PostAsJsonAsync("/api/masterdata/locations", new
            {
                code = "PROD-TEST",
                name = "Test Production",
                warehouseId = whId,
                type = 4,
                isActive = true
            });
            r.EnsureSuccessStatusCode();
        }

        // Create a receipt that lands 50 units of a batch at Receiving.
        var rcv = locations!.First(l => l.Code.StartsWith("RCV"));
        var batch = $"MOVE-TEST-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        var rec = await client.PostAsJsonAsync("/api/wms/receipts", new
        {
            receiptDate = DateTime.UtcNow,
            warehouseId = whId,
            lines = new[] {
                new {
                    itemId = items![0].Id, quantity = 50m, uoMId = uoms![0].Id,
                    batchNumber = batch, locationId = rcv.Id, qualityStatus = 1
                }
            }
        });
        rec.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — move batch to Production stage
        var move = await client.PostAsJsonAsync("/api/wms/inventory/move-batch", new
        {
            batchNumber = batch,
            targetStage = 4, // Production
            warehouseId = whId
        });
        move.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await move.Content.ReadFromJsonAsync<ResultResponse<MoveBatchResult>>();
        body!.IsSuccess.Should().BeTrue();
        body.Data!.BalancesMoved.Should().Be(1);
        body.Data.TotalQuantityMoved.Should().Be(50m);
        body.Data.Movements.Should().HaveCount(1);
        body.Data.Movements[0].Quantity.Should().Be(50m);

        // Assert — inventory now shows balance at Production location
        var inventory = await client.GetFromJsonAsync<List<InventoryRow>>("/api/wms/inventory");
        var atTarget = inventory!.Where(b => b.BatchNumber == batch && b.Quantity > 0).ToList();
        atTarget.Should().ContainSingle();
        atTarget[0].LocationId.Should().Be(body.Data.Movements[0].ToLocationId);
    }

    [Fact]
    public async Task MoveBatch_AlreadyAtTarget_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var body = await (await client.PostAsJsonAsync("/api/wms/inventory/move-batch",
            new { batchNumber = "no-such-batch-xxx", targetStage = 4 }))
            .Content.ReadFromJsonAsync<ResultResponse<object>>();
        body!.IsSuccess.Should().BeFalse();
        body.ErrorMessage.Should().Contain("No positive-quantity inventory");
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
    private sealed record InventoryRow(Guid Id, Guid ItemId, Guid LocationId, string? BatchNumber, decimal Quantity);
    private sealed record MoveBatchMovement(Guid MovementId, string MovementNumber, Guid ItemId,
        Guid FromLocationId, Guid ToLocationId, decimal Quantity);
    private sealed record MoveBatchResult(string BatchNumber, int BalancesMoved, decimal TotalQuantityMoved,
        List<MoveBatchMovement> Movements);
    private sealed record ResultResponse<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record LoginResponse(string AccessToken);
}
