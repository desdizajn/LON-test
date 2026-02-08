using System.Text.Json;
using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;

namespace LON.Infrastructure.Persistence;

/// <summary>
/// Seeder за Knowledge Base податоци (TARIC, Regulations, CodeLists, DeclarationRules)
/// </summary>
public static class KnowledgeBaseSeeder
{
    // Во Docker: /app/kb/processed, Локално: ../../kb/processed
    private static readonly string KbPath = Directory.Exists("/app/kb/processed") 
        ? "/app/kb/processed" 
        : Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "kb", "processed");
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task SeedKnowledgeBaseAsync(ApplicationDbContext context)
    {
        Console.WriteLine("🔄 Starting Knowledge Base seeding...");

        if (!await context.TariffCodes.AnyAsync())
        {
            await SeedTariffCodesAsync(context);
        }
        else
        {
            Console.WriteLine("✅ TariffCodes already seeded. Skipping.");
        }

        if (!await context.CustomsRegulations.AnyAsync())
        {
            await SeedCustomsRegulationsAsync(context);
        }
        else
        {
            Console.WriteLine("✅ CustomsRegulations already seeded. Skipping.");
        }

        if (!await context.CodeListItems.AnyAsync())
        {
            await SeedCodeListItemsAsync(context);
            await SeedAdditionalCodeListsAsync(context);
        }
        else
        {
            Console.WriteLine("✅ CodeListItems already seeded. Skipping.");
        }

        if (!await context.DeclarationRules.AnyAsync())
        {
            await SeedDeclarationRulesAsync(context);
        }
        else
        {
            Console.WriteLine("✅ DeclarationRules already seeded. Skipping.");
        }

        Console.WriteLine("✅ Knowledge Base seeding completed!");
    }

    #region TariffCodes (10,306 записи)

    private static async Task SeedTariffCodesAsync(ApplicationDbContext context)
    {
        Console.WriteLine("📦 Seeding TariffCodes from taric_data.json...");

        var jsonPath = Path.Combine(KbPath, "taric_data.json");
        
        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"⚠️ WARNING: File not found: {jsonPath}");
            return;
        }

        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        var taricData = JsonSerializer.Deserialize<List<TaricJsonModel>>(jsonContent, JsonOptions);

        if (taricData == null || !taricData.Any())
        {
            Console.WriteLine("⚠️ WARNING: No TARIC data found in JSON file.");
            return;
        }

        Console.WriteLine($"   Found {taricData.Count} TARIC records. Processing...");

        var tariffCodes = new List<TariffCode>();
        
        foreach (var item in taricData)
        {
            tariffCodes.Add(new TariffCode
            {
                Id = Guid.NewGuid(),
                TariffNumber = item.TariffNumber ?? string.Empty,
                TARBR = item.TARBR ?? string.Empty,
                TAROZ1 = item.TAROZ1 ?? string.Empty,
                TAROZ2 = item.TAROZ2 ?? string.Empty,
                TAROZ3 = item.TAROZ3 ?? string.Empty,
                Description = item.Description ?? string.Empty,
                CustomsRate = item.CustomsRate,
                UnitMeasure = item.UnitMeasure,
                VATRate = item.VATRate,
                IsActive = item.IsActive ?? true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "KnowledgeBaseSeeder"
            });
        }

        // Batch insert за брзина (1000 по 1000)
        var batchSize = 1000;
        for (int i = 0; i < tariffCodes.Count; i += batchSize)
        {
            var batch = tariffCodes.Skip(i).Take(batchSize).ToList();
            await context.TariffCodes.AddRangeAsync(batch);
            await context.SaveChangesAsync();
            Console.WriteLine($"   ✓ Inserted batch {i / batchSize + 1} ({batch.Count} records)");
        }

        Console.WriteLine($"✅ Successfully seeded {tariffCodes.Count} TariffCodes");
    }

    private class TaricJsonModel
    {
        public string? TariffNumber { get; set; }
        public string? TARBR { get; set; }
        public string? TAROZ1 { get; set; }
        public string? TAROZ2 { get; set; }
        public string? TAROZ3 { get; set; }
        public string? Description { get; set; }
        public decimal? CustomsRate { get; set; }
        public string? UnitMeasure { get; set; }
        public decimal? VATRate { get; set; }
        public bool? IsActive { get; set; }
    }

    #endregion

    #region CustomsRegulations (615 записи)

    private static async Task SeedCustomsRegulationsAsync(ApplicationDbContext context)
    {
        Console.WriteLine("📦 Seeding CustomsRegulations from regulations_data.json...");

        var jsonPath = Path.Combine(KbPath, "regulations_data.json");

        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"⚠️ WARNING: File not found: {jsonPath}");
            return;
        }

        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        var regulationsData = JsonSerializer.Deserialize<List<RegulationJsonModel>>(jsonContent, JsonOptions);

        if (regulationsData == null || !regulationsData.Any())
        {
            Console.WriteLine("⚠️ WARNING: No regulation data found in JSON file.");
            return;
        }

        Console.WriteLine($"   Found {regulationsData.Count} regulations. Processing...");

        var regulations = new List<CustomsRegulation>();

        foreach (var item in regulationsData)
        {
            // Зачисти TariffNumber - земи само први 10 карактери, игнорирај multiline записи
            string? tariffNumber = item.TariffNumber?.Trim();
            if (!string.IsNullOrEmpty(tariffNumber))
            {
                // Ако има нов ред, земи само првиот запис
                if (tariffNumber.Contains('\n'))
                {
                    tariffNumber = tariffNumber.Split('\n')[0].Trim();
                }
                
                // Ограничи на 10 карактери (валиден TARIC код)
                if (tariffNumber.Length > 10)
                {
                    tariffNumber = tariffNumber.Substring(0, 10);
                }
            }
            
            // Барај TariffCode ако постои валиден TariffNumber
            Guid? tariffCodeId = null;
            if (!string.IsNullOrEmpty(tariffNumber) && tariffNumber.Length == 10)
            {
                var tariffCode = await context.TariffCodes
                    .FirstOrDefaultAsync(t => t.TariffNumber == tariffNumber);
                tariffCodeId = tariffCode?.Id;
            }

            regulations.Add(new CustomsRegulation
            {
                Id = Guid.NewGuid(),
                CelexNumber = item.CelexNumber ?? string.Empty,
                TariffNumber = tariffNumber ?? string.Empty,
                TariffCodeId = tariffCodeId,
                DescriptionMK = item.DescriptionMK,
                DescriptionEN = item.DescriptionEN,
                LegalBasis = item.LegalBasis,
                EffectiveDate = item.EffectiveDate,
                ExpiryDate = item.ExpiryDate,
                IsActive = item.IsActive ?? true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "KnowledgeBaseSeeder"
            });
        }

        await context.CustomsRegulations.AddRangeAsync(regulations);
        await context.SaveChangesAsync();

        Console.WriteLine($"✅ Successfully seeded {regulations.Count} CustomsRegulations");
    }

    private class RegulationJsonModel
    {
        public string? CelexNumber { get; set; }
        public string? TariffNumber { get; set; }
        public string? DescriptionMK { get; set; }
        public string? DescriptionEN { get; set; }
        public string? LegalBasis { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool? IsActive { get; set; }
    }

    #endregion

    #region CodeListItems

    private static async Task SeedCodeListItemsAsync(ApplicationDbContext context)
    {
        var codeListItems = new List<CodeListItem>();

        // Try the richer lon_codelists_final.json first (16 code lists with Box numbers)
        var finalPath = Path.Combine(KbPath, "lon_codelists_final.json");
        if (File.Exists(finalPath))
        {
            Console.WriteLine("📦 Seeding CodeListItems from lon_codelists_final.json...");
            var jsonContent = await File.ReadAllTextAsync(finalPath);
            var wrapper = JsonSerializer.Deserialize<CodeListFinalWrapper>(jsonContent, JsonOptions);

            if (wrapper?.Codelists != null)
            {
                foreach (var kvp in wrapper.Codelists)
                {
                    var listType = kvp.Key;
                    var listData = kvp.Value;
                    if (listData?.Codes == null) continue;

                    foreach (var item in listData.Codes)
                    {
                        codeListItems.Add(new CodeListItem
                        {
                            Id = Guid.NewGuid(),
                            ListType = listType,
                            Code = item.Code ?? string.Empty,
                            DescriptionMK = item.DescriptionMK ?? string.Empty,
                            DescriptionEN = item.DescriptionEN,
                            IsActive = true,
                            SortOrder = item.SortOrder ?? 0,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "KnowledgeBaseSeeder"
                        });
                    }
                }
            }
        }

        // Fallback to simple lon_codelists.json
        if (codeListItems.Count == 0)
        {
            Console.WriteLine("📦 Seeding CodeListItems from lon_codelists.json...");
            var jsonPath = Path.Combine(KbPath, "lon_codelists.json");
            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"⚠️ WARNING: No code list files found in {KbPath}");
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var codeListData = JsonSerializer.Deserialize<Dictionary<string, List<CodeListJsonModel>>>(jsonContent, JsonOptions);

            if (codeListData == null || !codeListData.Any())
            {
                Console.WriteLine("⚠️ WARNING: No code list data found in JSON file.");
                return;
            }

            foreach (var listType in codeListData.Keys)
            {
                foreach (var item in codeListData[listType])
                {
                    codeListItems.Add(new CodeListItem
                    {
                        Id = Guid.NewGuid(),
                        ListType = listType,
                        Code = item.Code ?? string.Empty,
                        DescriptionMK = item.DescriptionMK ?? item.NameMK ?? string.Empty,
                        DescriptionEN = item.DescriptionEN ?? item.NameEN,
                        IsActive = item.IsActive ?? item.ValidForLON ?? true,
                        SortOrder = item.SortOrder ?? item.DisplayOrder ?? 0,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "KnowledgeBaseSeeder"
                    });
                }
            }
        }

        if (codeListItems.Count > 0)
        {
            await context.CodeListItems.AddRangeAsync(codeListItems);
            await context.SaveChangesAsync();
            Console.WriteLine($"✅ Successfully seeded {codeListItems.Count} CodeListItems");
        }
    }

    private class CodeListFinalWrapper
    {
        public Dictionary<string, CodeListFinalData>? Codelists { get; set; }
    }

    private class CodeListFinalData
    {
        public string? ListType { get; set; }
        public string? BoxNumber { get; set; }
        public int? TotalCodes { get; set; }
        public List<CodeListJsonModel>? Codes { get; set; }
    }

    private class CodeListJsonModel
    {
        public string? Code { get; set; }
        public string? DescriptionMK { get; set; }
        public string? DescriptionEN { get; set; }
        public string? NameMK { get; set; }
        public string? NameEN { get; set; }
        public bool? IsActive { get; set; }
        public bool? ValidForLON { get; set; }
        public int? SortOrder { get; set; }
        public int? DisplayOrder { get; set; }
    }

    #endregion

    #region DeclarationRules (17 правила)

    private static async Task SeedDeclarationRulesAsync(ApplicationDbContext context)
    {
        Console.WriteLine("📦 Seeding DeclarationRules from lon_validation_rules.json...");

        var jsonPath = Path.Combine(KbPath, "lon_validation_rules.json");

        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"⚠️ WARNING: File not found: {jsonPath}");
            return;
        }

        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        var rulesData = JsonSerializer.Deserialize<List<RuleJsonModel>>(jsonContent, JsonOptions);

        if (rulesData == null || !rulesData.Any())
        {
            Console.WriteLine("⚠️ WARNING: No validation rules found in JSON file.");
            return;
        }

        var declarationRules = new List<DeclarationRule>();

        foreach (var rule in rulesData)
        {
            declarationRules.Add(new DeclarationRule
            {
                Id = Guid.NewGuid(),
                RuleCode = rule.RuleCode ?? string.Empty,
                FieldName = rule.FieldName ?? string.Empty,
                RuleType = rule.RuleType ?? "General",
                ValidationLogic = rule.ValidationLogic ?? string.Empty,
                ErrorMessageMK = rule.ErrorMessageMK ?? rule.ErrorMessageEN ?? string.Empty,
                ErrorMessageEN = rule.ErrorMessageEN,
                Severity = rule.Severity ?? "Error",
                Priority = rule.Priority ?? 50,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "KnowledgeBaseSeeder"
            });
        }

        await context.DeclarationRules.AddRangeAsync(declarationRules);
        await context.SaveChangesAsync();

        Console.WriteLine($"✅ Successfully seeded {declarationRules.Count} DeclarationRules");
    }

    private class RuleJsonModel
    {
        public string? RuleCode { get; set; }
        public string? FieldName { get; set; }
        public string? RuleType { get; set; }
        public string? ValidationLogic { get; set; }
        public string? ErrorMessageMK { get; set; }
        public string? ErrorMessageEN { get; set; }
        public string? Severity { get; set; }
        public int? Priority { get; set; }
    }

    #endregion

    #region Additional CodeLists (Countries, Currencies, Customs Offices)

    private static async Task SeedAdditionalCodeListsAsync(ApplicationDbContext context)
    {
        var additionalFiles = new[]
        {
            ("countries_box15a_iso.json", "Box15a_CountryCode"),
            ("currencies_box22_iso.json", "Box22_Currency"),
            ("customs_offices_box29.json", "Box29_CustomsOffice"),
        };

        foreach (var (fileName, listType) in additionalFiles)
        {
            var filePath = Path.Combine(KbPath, fileName);
            if (!File.Exists(filePath)) continue;

            // Check if already seeded from lon_codelists_final.json
            if (await context.CodeListItems.AnyAsync(c => c.ListType == listType))
            {
                Console.WriteLine($"✅ {listType} already seeded from codelists_final. Skipping {fileName}.");
                continue;
            }

            Console.WriteLine($"📦 Seeding {listType} from {fileName}...");
            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<CodeListFinalData>(json, JsonOptions);

            if (data?.Codes == null || data.Codes.Count == 0) continue;

            var items = data.Codes.Select(c => new CodeListItem
            {
                Id = Guid.NewGuid(),
                ListType = listType,
                Code = c.Code ?? string.Empty,
                DescriptionMK = c.DescriptionMK ?? string.Empty,
                DescriptionEN = c.DescriptionEN,
                IsActive = true,
                SortOrder = c.SortOrder ?? 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "KnowledgeBaseSeeder"
            }).ToList();

            await context.CodeListItems.AddRangeAsync(items);
            await context.SaveChangesAsync();
            Console.WriteLine($"✅ Seeded {items.Count} items for {listType}");
        }
    }

    #endregion
}
