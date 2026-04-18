using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// The E2E smoke that would have caught the three P0.4/P0.6 bugs:
///   1. IApplicationDbContext incomplete -> handler couldn't persist
///   2. CreateReceiptCommandHandler missing AddAsync -> POST 200 but nothing saved
///   3. Receipt saved but InventoryBalance wasn't updated
/// </summary>
public class ReceiptFlowTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ReceiptFlowTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateReceipt_ThenGetInventory_BalanceReflectsQuantity()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        // Arrange — pull seeded master data IDs
        var items = await client.GetFromJsonAsync<List<Entity>>("/api/masterdata/items");
        var warehouses = await client.GetFromJsonAsync<List<Entity>>("/api/masterdata/warehouses");
        var uoms = await client.GetFromJsonAsync<List<Entity>>("/api/masterdata/uom");
        var partners = await client.GetFromJsonAsync<List<Entity>>("/api/masterdata/partners");
        var locations = await client.GetFromJsonAsync<List<EntityWithCode>>("/api/masterdata/locations");

        items.Should().NotBeNullOrEmpty("seed should insert sample items");
        warehouses.Should().NotBeNullOrEmpty("seed should insert a warehouse");
        uoms.Should().NotBeNullOrEmpty();
        partners.Should().NotBeNullOrEmpty();
        locations.Should().NotBeNullOrEmpty();

        var rcv = locations!.FirstOrDefault(l => l.Code.StartsWith("RCV", StringComparison.OrdinalIgnoreCase));
        rcv.Should().NotBeNull("a receiving location should be seeded");

        var itemId = items![0].Id;
        var warehouseId = warehouses![0].Id;
        var uomId = uoms![0].Id;
        var partnerId = partners![0].Id;

        // Act — POST receipt
        var payload = new
        {
            receiptDate = DateTime.UtcNow,
            warehouseId,
            partnerId,
            lines = new[] { new {
                itemId,
                quantity = 42.5m,
                uoMId = uomId,
                batchNumber = "BATCH-TEST-001",
                mrn = "26MK000012345678TEST",
                locationId = rcv!.Id,
                qualityStatus = 1, // OK
            }}
        };

        var post = await client.PostAsJsonAsync("/api/wms/receipts", payload);
        post.StatusCode.Should().Be(HttpStatusCode.OK);
        var postBody = await post.Content.ReadFromJsonAsync<ResultResponse>();
        postBody!.IsSuccess.Should().BeTrue();

        // Assert — receipt round-trips via GET
        var allReceipts = await client.GetFromJsonAsync<List<ReceiptListEntry>>("/api/wms/receipts");
        allReceipts.Should().ContainSingle(r => r.PurchaseOrderNumber == null
                                                || r.ReceiptNumber.StartsWith("RCP-"));

        // Assert — inventory balance reflects the receipt (catches the P0.6 gap)
        var inventory = await client.GetFromJsonAsync<List<InventoryBalanceRow>>("/api/wms/inventory");
        inventory.Should().NotBeNull();
        inventory!.Should().Contain(b =>
            b.ItemId == itemId &&
            b.LocationId == rcv.Id &&
            b.BatchNumber == "BATCH-TEST-001" &&
            b.Mrn == "26MK000012345678TEST" &&
            b.Quantity == 42.5m);
    }

    private async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record Entity(Guid Id);
    private sealed record EntityWithCode(Guid Id, string Code);
    private sealed record ResultResponse(bool IsSuccess, Guid Data, string? ErrorMessage);
    private sealed record ReceiptListEntry(Guid Id, string ReceiptNumber, string? PurchaseOrderNumber);
    private sealed record InventoryBalanceRow(Guid Id, Guid ItemId, Guid LocationId,
        string? BatchNumber, string? Mrn, decimal Quantity);
    private sealed record LoginResponse(string AccessToken);
}
