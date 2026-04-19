using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P5.1.2 — column mapping + named profiles.
///   1. Apply mapping without save -> session Status=Mapped, TargetEntity populated, Mapping echoed via GET.
///   2. Save mapping as profile -> suggestion endpoint returns it filtered by partner context.
///   3. Applying same (target, partner, label) again upserts UsageCount.
///   4. Unknown-source-column mapping -> 400.
///   5. Delete profile -> suggestion no longer includes it.
/// </summary>
public class ImportMappingTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ImportMappingTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ApplyMapping_NoProfile_SessionTransitionsToMapped()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var sessionId = await UploadCsvAsync(client, "Code,Qty\nA,1\nB,2\n", "items.csv");

        var mapping = new
        {
            mapping = new { columns = new[] {
                new { sourceHeader = "Code", targetField = "itemCode", ignore = false },
                new { sourceHeader = "Qty",  targetField = "quantity", ignore = false }
            }},
            targetEntity = "Items",
            partnerContextId = (Guid?)null,
            saveAsProfileLabel = (string?)null
        };

        var resp = await client.PutAsJsonAsync($"/api/import/sessions/{sessionId}/mapping", mapping);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await client.GetFromJsonAsync<ResultResponse<SessionResponse>>(
            $"/api/import/sessions/{sessionId}");
        session!.Data!.Status.Should().Be(2); // Mapped
        session.Data.TargetEntity.Should().Be("Items");
        session.Data.Mapping!.Columns.Should().HaveCount(2);
        session.Data.Mapping.Columns[0].SourceHeader.Should().Be("Code");
        session.Data.Mapping.Columns[0].TargetField.Should().Be("itemCode");
    }

    [Fact]
    public async Task SaveProfile_Twice_UpsertsUsageCount()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var sessionId = await UploadCsvAsync(client, "Code,Qty\nA,1\n", "items.csv");
        var partner = Guid.NewGuid();
        var payload = new
        {
            mapping = new { columns = new[] {
                new { sourceHeader = "Code", targetField = "itemCode", ignore = false },
                new { sourceHeader = "Qty",  targetField = "quantity", ignore = false }
            }},
            targetEntity = "Items",
            partnerContextId = (Guid?)partner,
            saveAsProfileLabel = "MAGNA invoice"
        };

        var first = await client.PutAsJsonAsync($"/api/import/sessions/{sessionId}/mapping", payload);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<ResultResponse<Guid>>();
        var profileId = firstBody!.Data;

        // Second session with same label → UsageCount increments on existing profile.
        var sessionId2 = await UploadCsvAsync(client, "Code,Qty\nB,2\n", "items2.csv");
        var second = await client.PutAsJsonAsync($"/api/import/sessions/{sessionId2}/mapping", payload);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        // Suggest returns exactly one profile for that (target + partner).
        var list = await client.GetFromJsonAsync<ResultResponse<List<ProfileRow>>>(
            $"/api/import/mapping-profiles?targetEntity=Items&partnerContextId={partner}");
        list!.IsSuccess.Should().BeTrue();
        var rows = list.Data!;
        rows.Should().ContainSingle(p => p.Id == profileId);
        rows.Single(p => p.Id == profileId).UsageCount.Should().Be(2);
    }

    [Fact]
    public async Task Suggest_PrefersPartnerSpecificOverGeneric()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var partner = Guid.NewGuid();
        var s1 = await UploadCsvAsync(client, "A,B\n1,2\n", "generic.csv");
        await client.PutAsJsonAsync($"/api/import/sessions/{s1}/mapping", new {
            mapping = new { columns = new[] { new { sourceHeader = "A", targetField = "a", ignore = false } } },
            targetEntity = "Items",
            partnerContextId = (Guid?)null,
            saveAsProfileLabel = "generic"
        });
        var s2 = await UploadCsvAsync(client, "A,B\n1,2\n", "partner.csv");
        await client.PutAsJsonAsync($"/api/import/sessions/{s2}/mapping", new {
            mapping = new { columns = new[] { new { sourceHeader = "A", targetField = "a", ignore = false } } },
            targetEntity = "Items",
            partnerContextId = (Guid?)partner,
            saveAsProfileLabel = "MAGNA"
        });

        var list = await client.GetFromJsonAsync<ResultResponse<List<ProfileRow>>>(
            $"/api/import/mapping-profiles?targetEntity=Items&partnerContextId={partner}");
        list!.IsSuccess.Should().BeTrue();
        list.Data!.Should().HaveCountGreaterOrEqualTo(2);
        list.Data![0].PartnerContextId.Should().Be(partner, "partner-specific profile is preferred");
    }

    [Fact]
    public async Task Mapping_UnknownSourceHeader_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var sessionId = await UploadCsvAsync(client, "Code,Qty\nA,1\n", "bad.csv");

        var resp = await client.PutAsJsonAsync($"/api/import/sessions/{sessionId}/mapping", new {
            mapping = new { columns = new[] { new { sourceHeader = "Missing", targetField = "x", ignore = false } } },
            targetEntity = "Items",
            partnerContextId = (Guid?)null,
            saveAsProfileLabel = (string?)null
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteProfile_Removes_FromSuggestions()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var partner = Guid.NewGuid();
        var sessionId = await UploadCsvAsync(client, "A,B\n1,2\n", "delme.csv");
        var save = await client.PutAsJsonAsync($"/api/import/sessions/{sessionId}/mapping", new {
            mapping = new { columns = new[] { new { sourceHeader = "A", targetField = "a", ignore = false } } },
            targetEntity = "Partners",
            partnerContextId = (Guid?)partner,
            saveAsProfileLabel = "tobe-deleted"
        });
        var profileId = (await save.Content.ReadFromJsonAsync<ResultResponse<Guid>>())!.Data;

        var del = await client.DeleteAsync($"/api/import/mapping-profiles/{profileId}");
        del.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetFromJsonAsync<ResultResponse<List<ProfileRow>>>(
            $"/api/import/mapping-profiles?targetEntity=Partners&partnerContextId={partner}");
        list!.Data.Should().NotContain(p => p.Id == profileId);
    }

    // ---------- helpers ----------

    private static async Task<Guid> UploadCsvAsync(HttpClient client, string content, string fileName)
    {
        using var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        multipart.Add(file, "file", fileName);
        var resp = await client.PostAsync("/api/import/sessions", multipart);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ResultResponse<SessionResponse>>();
        return body!.Data!.Id;
    }

    private static async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record SessionResponse(
        Guid Id, int Status, string? TargetEntity, MappingRow? Mapping);
    private sealed record MappingRow(List<MappingColumnRow> Columns);
    private sealed record MappingColumnRow(string SourceHeader, string? TargetField, bool Ignore);
    private sealed record ProfileRow(Guid Id, string Label, Guid? PartnerContextId, int UsageCount, MappingRow Mapping);
    private sealed record ResultResponse<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record LoginResponse(string AccessToken);
}
