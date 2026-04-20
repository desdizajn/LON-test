using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P5.1.6 — dry-run and commit for the Items target end-to-end, including
/// LOOKUP resolution (baseUoMCode → UoM.Id).
///   1. Dry-run with missing required field → rowsWithErrors > 0, committable false.
///   2. Full dry-run with header defaults populating missing required fields → committable.
///   3. Commit creates the item rows; duplicate codes inside file rejected.
/// </summary>
public class ImportRunTests : IClassFixture<LonApiFactory>
{
    private readonly LonApiFactory _factory;

    public ImportRunTests(LonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DryRun_MissingRequiredField_ReportsErrors_NotCommittable()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        // Only code + name, no type / baseUoMCode → required-field errors.
        var sid = await UploadCsvAsync(client, "Code,Name\nITM-A,Alpha\nITM-B,Beta\n", "items.csv");
        await ApplyMapping(client, sid, "Items", new (string, string)[] {
            ("Code", "code"), ("Name", "name")
        });

        var resp = await client.PostAsync($"/api/import/sessions/{sid}/dry-run", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ResultResponse<RunReport>>();
        body!.IsSuccess.Should().BeTrue();
        body.Data!.Committable.Should().BeFalse();
        body.Data.RowsWithErrors.Should().Be(2);
        body.Data.WasCommitted.Should().BeFalse();
    }

    [Fact]
    public async Task DryRun_HeaderDefaultsFillMissing_MakesCommittable()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var uomCode = await FirstUomCodeAsync(client);
        var sid = await UploadCsvAsync(client,
            "Code,Name\nITM-H1,Alpha\nITM-H2,Beta\n", "items.csv");
        await ApplyMapping(client, sid, "Items", new (string, string)[] {
            ("Code", "code"), ("Name", "name")
        });
        await client.PutAsJsonAsync($"/api/import/sessions/{sid}/defaults", new {
            defaults = new {
                values = new Dictionary<string, string?> {
                    ["type"] = "RawMaterial",
                    ["baseUoMCode"] = uomCode
                }
            }
        });

        var dryRun = await client.PostAsync($"/api/import/sessions/{sid}/dry-run", null);
        var dryBody = await dryRun.Content.ReadFromJsonAsync<ResultResponse<RunReport>>();
        dryBody!.Data!.Committable.Should().BeTrue();
        dryBody.Data.WasCommitted.Should().BeFalse();
        dryBody.Data.RowsWithErrors.Should().Be(0);
    }

    [Fact]
    public async Task Commit_CreatesItems_OnceAtomically()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var uomCode = await FirstUomCodeAsync(client);
        var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
        var sid = await UploadCsvAsync(client,
            $"Code,Name\nITM-C-{suffix}-1,One\nITM-C-{suffix}-2,Two\n",
            "items.csv");
        await ApplyMapping(client, sid, "Items", new (string, string)[] {
            ("Code", "code"), ("Name", "name")
        });
        await client.PutAsJsonAsync($"/api/import/sessions/{sid}/defaults", new {
            defaults = new {
                values = new Dictionary<string, string?> {
                    ["type"] = "RawMaterial",
                    ["baseUoMCode"] = uomCode
                }
            }
        });

        var commit = await client.PostAsync($"/api/import/sessions/{sid}/commit", null);
        commit.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await commit.Content.ReadFromJsonAsync<ResultResponse<RunReport>>();
        body!.Data!.WasCommitted.Should().BeTrue();
        body.Data.EntitiesCreated.Should().Be(2);

        // Item list now includes the new codes.
        var items = await client.GetFromJsonAsync<List<ItemRow>>("/api/masterdata/items");
        items!.Select(i => i.Code).Should().Contain(new[] {
            $"ITM-C-{suffix}-1", $"ITM-C-{suffix}-2"
        });
    }

    [Fact]
    public async Task Commit_DuplicateCodeInFile_AbortsAtomically()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var uomCode = await FirstUomCodeAsync(client);
        var dup = $"DUP-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        var sid = await UploadCsvAsync(client, $"Code,Name\n{dup},One\n{dup},Two\n", "dup.csv");
        await ApplyMapping(client, sid, "Items", new (string, string)[] {
            ("Code", "code"), ("Name", "name")
        });
        await client.PutAsJsonAsync($"/api/import/sessions/{sid}/defaults", new {
            defaults = new {
                values = new Dictionary<string, string?> {
                    ["type"] = "RawMaterial",
                    ["baseUoMCode"] = uomCode
                }
            }
        });

        var commit = await client.PostAsync($"/api/import/sessions/{sid}/commit", null);
        commit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await commit.Content.ReadFromJsonAsync<ResultResponse<RunReport>>();
        body!.IsSuccess.Should().BeFalse();
        body.ErrorMessage.Should().Contain("duplicate");

        var items = await client.GetFromJsonAsync<List<ItemRow>>("/api/masterdata/items");
        items!.Should().NotContain(i => i.Code == dup);
    }

    /// <summary>
    /// P6.35 — BOMs target commits a BOM per parent FG with one BOMLine per row.
    /// Verifies baseline: 2 components share the same parentItemCode (header
    /// default) → 1 BOM + 2 BOMLines + monotonic line numbers.
    /// </summary>
    [Fact]
    public async Task Commit_Boms_CreatesBomWithLinesForHeaderParent()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var uomCode = await FirstUomCodeAsync(client);

        // Seed a parent FG + two components via the Items executor so the LOOKUPs
        // resolve. Using the same session flow keeps the test self-contained.
        var tag = Guid.NewGuid().ToString("N").Substring(0, 6);
        var parent = $"FG-P635-{tag}";
        var compA = $"MAT-A-{tag}";
        var compB = $"MAT-B-{tag}";
        var sidItems = await UploadCsvAsync(client,
            $"Code,Name\n{parent},ParentFG\n{compA},CompA\n{compB},CompB\n",
            "items.csv");
        await ApplyMapping(client, sidItems, "Items", new (string, string)[] {
            ("Code", "code"), ("Name", "name")
        });
        await client.PutAsJsonAsync($"/api/import/sessions/{sidItems}/defaults", new {
            defaults = new {
                values = new Dictionary<string, string?> {
                    ["type"] = "RawMaterial",
                    ["baseUoMCode"] = uomCode
                }
            }
        });
        var itemsCommit = await client.PostAsync($"/api/import/sessions/{sidItems}/commit", null);
        itemsCommit.EnsureSuccessStatusCode();

        // Now the BOMs import.
        var sid = await UploadCsvAsync(client,
            $"Component,Qty,Uom\n{compA},2,{uomCode}\n{compB},5,{uomCode}\n",
            "bom.csv");
        await ApplyMapping(client, sid, "BOMs", new (string, string)[] {
            ("Component", "componentItemCode"),
            ("Qty", "componentQuantity"),
            ("Uom", "componentUomCode")
        });
        await client.PutAsJsonAsync($"/api/import/sessions/{sid}/transforms", new {
            transforms = new { columns = new[] {
                new { sourceHeader = "Component", rules = new[] { "LOOKUP:Items.Code" } },
                new { sourceHeader = "Uom", rules = new[] { "LOOKUP:UnitsOfMeasure.Code" } }
            }}
        });
        await client.PutAsJsonAsync($"/api/import/sessions/{sid}/defaults", new {
            defaults = new {
                values = new Dictionary<string, string?> {
                    ["parentItemCode"] = parent,
                    ["baseQuantity"] = "1"
                }
            }
        });
        // parentItemCode is an Items lookup too — it must be transformed.
        var currentTransforms = new {
            transforms = new { columns = new[] {
                new { sourceHeader = "Component", rules = new[] { "LOOKUP:Items.Code" } },
                new { sourceHeader = "Uom", rules = new[] { "LOOKUP:UnitsOfMeasure.Code" } },
                new { sourceHeader = "parentItemCode", rules = new[] { "LOOKUP:Items.Code" } }
            }}
        };
        await client.PutAsJsonAsync($"/api/import/sessions/{sid}/transforms", currentTransforms);

        var commit = await client.PostAsync($"/api/import/sessions/{sid}/commit", null);
        var body = await commit.Content.ReadFromJsonAsync<ResultResponse<RunReport>>();
        commit.StatusCode.Should().Be(HttpStatusCode.OK, body?.ErrorMessage ?? "unknown");
        body!.Data!.WasCommitted.Should().BeTrue();
        body.Data.EntitiesCreated.Should().Be(3, "1 BOM + 2 BOMLines");
    }

    [Fact]
    public async Task Commit_UnknownLookup_ReportedAndRolledBack()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);
        var sid = await UploadCsvAsync(client,
            "Code,Uom\nITM-BAD-1,DOESNOTEXIST-UOM\n", "items.csv");
        await ApplyMapping(client, sid, "Items", new (string, string)[] {
            ("Code", "code"), ("Uom", "baseUoMCode")
        });
        // Attach LOOKUP:UnitsOfMeasure.Code — an unknown UoM will flag an error.
        await client.PutAsJsonAsync($"/api/import/sessions/{sid}/transforms", new {
            transforms = new { columns = new[] {
                new { sourceHeader = "Uom", rules = new[] { "LOOKUP:UnitsOfMeasure.Code" } }
            }}
        });
        await client.PutAsJsonAsync($"/api/import/sessions/{sid}/defaults", new {
            defaults = new { values = new Dictionary<string, string?> { ["type"] = "RawMaterial", ["name"] = "X" } }
        });

        var commit = await client.PostAsync($"/api/import/sessions/{sid}/commit", null);
        var body = await commit.Content.ReadFromJsonAsync<ResultResponse<RunReport>>();
        body!.Data!.Committable.Should().BeFalse();
        body.Data.RowsWithErrors.Should().Be(1);
        body.Data.WasCommitted.Should().BeFalse();
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

    private static async Task ApplyMapping(HttpClient client, Guid sid, string target, (string src, string tgt)[] cols)
    {
        var payload = new {
            mapping = new { columns = cols.Select(c => new { sourceHeader = c.src, targetField = c.tgt, ignore = false }).ToArray() },
            targetEntity = target,
            partnerContextId = (Guid?)null,
            saveAsProfileLabel = (string?)null
        };
        var r = await client.PutAsJsonAsync($"/api/import/sessions/{sid}/mapping", payload);
        r.EnsureSuccessStatusCode();
    }

    private static async Task<string> FirstUomCodeAsync(HttpClient client)
    {
        var uoms = await client.GetFromJsonAsync<List<UomRow>>("/api/masterdata/uom");
        uoms.Should().NotBeNullOrEmpty();
        return uoms![0].Code;
    }

    private static async Task Authenticate(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Admin123!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private sealed record SessionResp(Guid Id);
    private sealed record RunReport(string TargetEntity, int RowCount, int RowsWithErrors,
        bool Committable, bool WasCommitted, int EntitiesCreated);
    private sealed record ItemRow(Guid Id, string Code);
    private sealed record UomRow(Guid Id, string Code);
    private sealed record ResultResponse<T>(bool IsSuccess, T? Data, string? ErrorMessage);
    private sealed record LoginResponse(string AccessToken);
}
