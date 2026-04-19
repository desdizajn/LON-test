using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P5.1.3 + P5.1.4 — header-level defaults and per-column transforms.
///   1. PUT /defaults persists + strips empty-string entries; GET echoes.
///   2. PUT /transforms validates column headers; GET echoes rules.
///   3. Preview applies UPPER + TRIM + DECIMAL_COMMA_TO_DOT + DATE_PARSE in order.
///   4. LOOKUP rule is noop at preview (no DB access yet).
///   5. Transform on missing column -> 400.
/// </summary>
public class ImportDefaultsAndTransformsTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ImportDefaultsAndTransformsTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SetDefaults_EchoesBackStrippingEmptyValues()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var sid = await UploadCsvAsync(client, "Code,Qty\nA,1\n", "d.csv");

        var resp = await client.PutAsJsonAsync($"/api/import/sessions/{sid}/defaults", new {
            defaults = new {
                values = new Dictionary<string, string?>
                {
                    ["warehouseId"] = "wh-1",
                    ["locationId"] = "",              // stripped
                    ["mrn"] = "26MK0001",
                    ["receiptDate"] = "2026-04-19"
                }
            }
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await client.GetFromJsonAsync<ResultResponse<SessionResp>>(
            $"/api/import/sessions/{sid}");
        var def = session!.Data!.Defaults!.Values;
        def.Should().ContainKey("warehouseId").WhoseValue.Should().Be("wh-1");
        def.Should().ContainKey("mrn").WhoseValue.Should().Be("26MK0001");
        def.Should().NotContainKey("locationId", "empty string is stripped");
    }

    [Fact]
    public async Task SetTransforms_Echo_And_Preview_Applies_Rules()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var sid = await UploadCsvAsync(client,
            "Code,Qty,Dt\n a ,1,23\n b ,\"2,5\",01.05.2026\n",
            "t.csv");

        var put = await client.PutAsJsonAsync($"/api/import/sessions/{sid}/transforms", new {
            transforms = new {
                columns = new[] {
                    new { sourceHeader = "Code", rules = new[] { "TRIM", "UPPER" } },
                    new { sourceHeader = "Qty",  rules = new[] { "DECIMAL_COMMA_TO_DOT" } },
                    new { sourceHeader = "Dt",   rules = new[] { "DATE_PARSE:dd.MM.yyyy" } }
                }
            }
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var preview = await client.GetFromJsonAsync<ResultResponse<PreviewResp>>(
            $"/api/import/sessions/{sid}/preview-transformed");
        preview!.IsSuccess.Should().BeTrue();
        var rows = preview.Data!.Rows;
        rows.Should().HaveCount(2);

        // Row 0: " a ", "1", "23" — UPPER+TRIM gives "A", DECIMAL_COMMA_TO_DOT leaves "1",
        // DATE_PARSE:dd.MM.yyyy on "23" fails silently so stays "23".
        rows[0].Should().StartWith(new[] { "A", "1", "23" });
        // Row 1: " b " → "B"; "2,5" → "2.5"; "01.05.2026" → parsed to ISO like "2026-05-01T00:00:00.0000000".
        rows[1][0].Should().Be("B");
        rows[1][1].Should().Be("2.5");
        rows[1][2].Should().StartWith("2026-05-01");
    }

    [Fact]
    public async Task SetTransforms_UnknownHeader_Returns400()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var sid = await UploadCsvAsync(client, "Code\nA\n", "bad.csv");

        var resp = await client.PutAsJsonAsync($"/api/import/sessions/{sid}/transforms", new {
            transforms = new {
                columns = new[] { new { sourceHeader = "Missing", rules = new[] { "TRIM" } } }
            }
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LookupRule_IsNoOpAtPreview()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var sid = await UploadCsvAsync(client, "PartnerCode\nMAGNA\n", "l.csv");

        await client.PutAsJsonAsync($"/api/import/sessions/{sid}/transforms", new {
            transforms = new {
                columns = new[] { new { sourceHeader = "PartnerCode", rules = new[] { "LOOKUP:Partners.Code" } } }
            }
        });
        var preview = await client.GetFromJsonAsync<ResultResponse<PreviewResp>>(
            $"/api/import/sessions/{sid}/preview-transformed");
        preview!.Data!.Rows[0][0].Should().Be("MAGNA", "LOOKUP defers to commit; preview leaves value untouched");
    }

    // helpers
    private static async Task<Guid> UploadCsvAsync(HttpClient client, string content, string fileName)
    {
        using var m = new MultipartFormDataContent();
        var f = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        f.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        m.Add(f, "file", fileName);
        var r = await client.PostAsync("/api/import/sessions", m);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<ResultResponse<SessionResp>>())!.Data!.Id;
    }
    private static async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record SessionResp(Guid Id, DefaultsRow? Defaults);
    private sealed record DefaultsRow(Dictionary<string, string?> Values);
    private sealed record PreviewResp(List<string> Headers, List<List<string?>> Rows);
    private sealed record ResultResponse<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record LoginResponse(string AccessToken);
}
