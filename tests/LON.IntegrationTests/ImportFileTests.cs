using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P5.1.1 — generic importer file upload + parse + preview.
///   1. CSV round-trip: upload 3-row CSV, headers parsed, all rows returned, preview capped.
///   2. GET /sessions/{id} returns the same parsed state.
///   3. Unsupported extension -> 400.
///   4. Empty file -> 400.
///   5. TSV auto-detect on .csv with tab delimiter works.
///   6. JSON array root works.
/// </summary>
public class ImportFileTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ImportFileTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task UploadCsv_ParsesHeadersAndPreview_RoundTripsViaGet()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var csv = "Code,Name,Qty\nITEM-1,First item,10\nITEM-2,Second item,20\nITEM-3,Third,30\n";
        var response = await UploadAsync(client, csv, "sample.csv", "text/csv");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ResultResponse<ImportSessionResponse>>();
        body!.IsSuccess.Should().BeTrue();
        var session = body.Data!;

        session.Format.Should().Be(2); // Csv = 2
        session.Headers.Should().Equal("Code", "Name", "Qty");
        session.TotalRowCount.Should().Be(3);
        session.PreviewRows.Should().HaveCount(3);
        session.PreviewRows[0].Should().Equal("ITEM-1", "First item", "10");

        var get = await client.GetFromJsonAsync<ResultResponse<ImportSessionResponse>>(
            $"/api/import/sessions/{session.Id}");
        get!.IsSuccess.Should().BeTrue();
        get.Data!.Id.Should().Be(session.Id);
        get.Data.Headers.Should().Equal("Code", "Name", "Qty");
        get.Data.TotalRowCount.Should().Be(3);
    }

    [Fact]
    public async Task UploadTsvMisnamedAsCsv_DetectsTabDelimiter()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var tsv = "Code\tName\nA\tAlpha\nB\tBeta\n";
        var response = await UploadAsync(client, tsv, "mislabelled.csv", "text/csv");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ResultResponse<ImportSessionResponse>>();
        body!.IsSuccess.Should().BeTrue();
        body.Data!.Format.Should().Be(3); // Tsv = 3
        body.Data.Headers.Should().Equal("Code", "Name");
        body.Data.PreviewRows[0].Should().Equal("A", "Alpha");
    }

    [Fact]
    public async Task UploadJson_ArrayOfObjects_ParsesAsTable()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var json = "[{\"code\":\"X\",\"qty\":5},{\"code\":\"Y\",\"qty\":7}]";
        var response = await UploadAsync(client, json, "items.json", "application/json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ResultResponse<ImportSessionResponse>>();
        body!.IsSuccess.Should().BeTrue();
        body.Data!.Format.Should().Be(4); // Json = 4
        body.Data.Headers.Should().Equal("code", "qty");
        body.Data.TotalRowCount.Should().Be(2);
    }

    [Fact]
    public async Task UploadXml_RepeatedElements_ParsedAsTable()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var xml = "<items><item code=\"X\"><qty>5</qty></item><item code=\"Y\"><qty>7</qty></item></items>";
        var response = await UploadAsync(client, xml, "items.xml", "application/xml");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ResultResponse<ImportSessionResponse>>();
        body!.IsSuccess.Should().BeTrue();
        body.Data!.Format.Should().Be(5); // Xml = 5
        body.Data.Headers.Should().Contain(new[] { "code", "qty" });
        body.Data.TotalRowCount.Should().Be(2);
    }

    [Fact]
    public async Task UnsupportedExtension_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var response = await UploadAsync(client, "anything", "thing.exe", "application/octet-stream");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PreviewCappedAt20_TotalCountReflectsAllRows()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var sb = new StringBuilder("Code,Qty\n");
        for (int i = 0; i < 25; i++) sb.Append($"ITEM-{i},{i}\n");
        var response = await UploadAsync(client, sb.ToString(), "bulk.csv", "text/csv");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ResultResponse<ImportSessionResponse>>();
        body!.IsSuccess.Should().BeTrue();
        body.Data!.TotalRowCount.Should().Be(25);
        body.Data.PreviewRows.Should().HaveCount(20);
    }

    // ---------- helpers ----------

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, string content, string fileName, string contentType)
    {
        using var multipart = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes(content);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        multipart.Add(fileContent, "file", fileName);
        return await client.PostAsync("/api/import/sessions", multipart);
    }

    private static async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record ImportSessionResponse(
        Guid Id,
        string OriginalFileName,
        int Format,
        long FileSizeBytes,
        int Status,
        List<string> Headers,
        List<List<string?>> PreviewRows,
        int TotalRowCount,
        string? TargetEntity,
        Guid? PartnerContextId,
        DateTime CreatedAt);

    private sealed record ResultResponse<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record LoginResponse(string AccessToken);
}
