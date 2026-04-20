using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LON.Domain.Enums;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P5.3.4 regression guard — GET /api/MasterData/items/article-picker.
///
/// Shape we care about:
///   1. A query that matches a base item also returns its A-suffix sibling
///      if one exists, grouped under the same base code.
///   2. A query that matches only the A-suffix returns the base sibling
///      alongside it (the normalised grouping is symmetric).
/// </summary>
public class ArticlePickerTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ArticlePickerTests(LonApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthedAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Picker_Returns_Base_AndASuffix_Together()
    {
        var client = await AuthedAsync();
        var uoms = await client.GetFromJsonAsync<List<UoMRow>>("/api/MasterData/uom");
        var uom = uoms!.First(u => u.IsActive).Id;

        // Seed one base + one A-suffix sibling. Unique shared prefix.
        var basePrefix = $"AP-{Guid.NewGuid():N}".Substring(0, 10);
        var baseCode = basePrefix;
        var aCode = basePrefix + "A";

        var createBase = await client.PostAsJsonAsync("/api/MasterData/items", new
        {
            code = baseCode,
            name = "Base article",
            description = "base",
            itemType = ItemType.RawMaterial,
            uoMId = uom,
            isBatchRequired = false,
            isMRNRequired = false,
            hsCode = "11111111",
            isActive = true
        });
        createBase.StatusCode.Should().Be(HttpStatusCode.OK);

        var createA = await client.PostAsJsonAsync("/api/MasterData/items", new
        {
            code = aCode,
            name = "A-suffix sibling",
            description = "a-suffix",
            itemType = ItemType.RawMaterial,
            uoMId = uom,
            isBatchRequired = false,
            isMRNRequired = false,
            hsCode = "22222222",
            isActive = true
        });
        createA.StatusCode.Should().Be(HttpStatusCode.OK);

        // Query by the base prefix — both variants should surface grouped under basePrefix.
        var resp = await client.GetAsync(
            $"/api/MasterData/items/article-picker?query={Uri.EscapeDataString(basePrefix)}&limit=20");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var groups = await resp.Content.ReadFromJsonAsync<List<PickerGroup>>();
        groups.Should().NotBeNull();
        var group = groups!.FirstOrDefault(g => g.BaseCode == basePrefix);
        group.Should().NotBeNull("base-code group must be present");
        group!.Variants.Should().HaveCountGreaterOrEqualTo(2);
        group.Variants.Any(v => v.Code == baseCode && !v.IsASuffix).Should().BeTrue();
        group.Variants.Any(v => v.Code == aCode && v.IsASuffix).Should().BeTrue();

        // Query specifically for the A-suffix — the base should still be
        // pulled in as a sibling and grouped under the same base key.
        var respA = await client.GetAsync(
            $"/api/MasterData/items/article-picker?query={Uri.EscapeDataString(aCode)}&limit=20");
        respA.StatusCode.Should().Be(HttpStatusCode.OK);
        var groupsA = await respA.Content.ReadFromJsonAsync<List<PickerGroup>>();
        var groupA = groupsA!.FirstOrDefault(g => g.BaseCode == basePrefix);
        groupA.Should().NotBeNull();
        groupA!.Variants.Any(v => v.Code == baseCode).Should().BeTrue();
        groupA.Variants.Any(v => v.Code == aCode).Should().BeTrue();
    }

    private sealed record LoginResponse(string AccessToken);
    private sealed record UoMRow(Guid Id, string Code, bool IsActive);
    private sealed record PickerGroup(string BaseCode, List<PickerVariant> Variants);
    private sealed record PickerVariant(Guid Id, string Code, string Name, string? HsCode, string? CountryOfOrigin, bool IsASuffix);
}
