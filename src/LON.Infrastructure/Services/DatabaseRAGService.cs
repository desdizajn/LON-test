using LON.Application.Common.Interfaces;
using LON.Application.KnowledgeBase.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LON.Infrastructure.Services;

/// <summary>
/// Database-based RAG Service - works without OpenAI by searching directly in DB tables.
/// Searches TariffCodes, CustomsRegulations, CodeListItems, DeclarationRules.
/// </summary>
public class DatabaseRAGService : IRAGService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DatabaseRAGService> _logger;

    public DatabaseRAGService(
        IApplicationDbContext context,
        ILogger<DatabaseRAGService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<RAGResponse> AskQuestionAsync(string question, int maxContextChunks = 5)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return new RAGResponse
            {
                Success = false,
                ErrorMessage = "Прашањето не може да биде празно"
            };
        }

        try
        {
            var sources = new List<SourceReference>();
            var answerParts = new List<string>();
            var q = question.ToLower();

            // Extract potential tariff number from question
            var tariffMatch = System.Text.RegularExpressions.Regex.Match(question, @"\b(\d{4}[\s.]?\d{2}[\s.]?\d{2,4})\b");
            var tariffNumber = tariffMatch.Success ? tariffMatch.Value.Replace(" ", "").Replace(".", "") : null;

            // 1. Search TariffCodes if question mentions tariff/HS code/тарифна
            if (tariffNumber != null || ContainsAny(q, "тариф", "tarif", "hs код", "hs code", "царин", "стапк", "box 33", "box33", "тарбр"))
            {
                var tariffResults = await SearchTariffCodesAsync(question, tariffNumber, maxContextChunks);
                if (tariffResults.Any())
                {
                    answerParts.Add("## Тарифни ознаки\n");
                    foreach (var t in tariffResults)
                    {
                        answerParts.Add($"**{t.TariffNumber}** - {t.Description}");
                        if (t.CustomsRate.HasValue)
                            answerParts.Add($"  - Царинска стапка: {t.CustomsRate}%");
                        if (t.VATRate.HasValue)
                            answerParts.Add($"  - ДДВ: {t.VATRate}%");
                        if (!string.IsNullOrEmpty(t.UnitMeasure))
                            answerParts.Add($"  - Единица мерка: {t.UnitMeasure}");
                        answerParts.Add("");

                        sources.Add(new SourceReference
                        {
                            DocumentTitle = "TARIC База",
                            Reference = $"Тарифна ознака {t.TariffNumber}",
                            ContentSnippet = $"{t.TariffNumber} - {t.Description}",
                            RelevanceScore = 0.95
                        });
                    }
                }
            }

            // 2. Search Regulations
            if (ContainsAny(q, "регулатив", "пропис", "закон", "правилник", "уредба", "regulation", "celex",
                "облагородување", "inward", "привремен", "увоз", "извоз", "export", "import",
                "процедур", "режим", "box", "sad", "садка", "мрд", "mrn", "гаранц", "guarantee"))
            {
                var regResults = await SearchRegulationsAsync(question, tariffNumber, maxContextChunks);
                if (regResults.Any())
                {
                    answerParts.Add("## Регулативи\n");
                    foreach (var r in regResults)
                    {
                        var desc = r.DescriptionMK ?? r.DescriptionEN ?? "";
                        answerParts.Add($"**{r.CelexNumber}** (Тариф. {r.TariffNumber})");
                        if (!string.IsNullOrEmpty(desc))
                            answerParts.Add($"  {(desc.Length > 300 ? desc.Substring(0, 300) + "..." : desc)}");
                        if (!string.IsNullOrEmpty(r.LegalBasis))
                            answerParts.Add($"  - Правна основа: {r.LegalBasis}");
                        answerParts.Add("");

                        sources.Add(new SourceReference
                        {
                            DocumentTitle = "Царински регулативи",
                            Reference = r.CelexNumber,
                            ContentSnippet = desc.Length > 200 ? desc.Substring(0, 200) + "..." : desc,
                            RelevanceScore = 0.9
                        });
                    }
                }
            }

            // 3. Search CodeLists
            if (ContainsAny(q, "код", "code", "листа", "list", "процедур", "procedure", "документ", "document",
                "транспорт", "transport", "пакување", "package", "box", "валута", "currency", "земја", "country",
                "царинарница", "office"))
            {
                var codeResults = await SearchCodeListsAsync(question, maxContextChunks);
                if (codeResults.Any())
                {
                    var grouped = codeResults.GroupBy(c => c.ListType);
                    answerParts.Add("## Кодни листи\n");
                    foreach (var group in grouped)
                    {
                        answerParts.Add($"### {group.Key}\n");
                        foreach (var c in group)
                        {
                            answerParts.Add($"- **{c.Code}** - {c.DescriptionMK}");
                            sources.Add(new SourceReference
                            {
                                DocumentTitle = $"Кодна листа: {group.Key}",
                                Reference = c.Code,
                                ContentSnippet = $"{c.Code} - {c.DescriptionMK}",
                                RelevanceScore = 0.85
                            });
                        }
                        answerParts.Add("");
                    }
                }
            }

            // 4. Search DeclarationRules
            if (ContainsAny(q, "правил", "rule", "валидац", "valid", "грешк", "error", "box", "поле", "field",
                "декларац", "declaration", "проверк", "check"))
            {
                var ruleResults = await SearchDeclarationRulesAsync(question, maxContextChunks);
                if (ruleResults.Any())
                {
                    answerParts.Add("## Правила за валидација\n");
                    foreach (var r in ruleResults)
                    {
                        answerParts.Add($"**{r.RuleCode}** ({r.FieldName}) - {r.RuleType}");
                        answerParts.Add($"  - {r.ErrorMessageMK}");
                        if (!string.IsNullOrEmpty(r.ValidationLogic))
                            answerParts.Add($"  - Логика: {r.ValidationLogic}");
                        answerParts.Add("");

                        sources.Add(new SourceReference
                        {
                            DocumentTitle = "Правила за валидација",
                            Reference = r.RuleCode,
                            ContentSnippet = r.ErrorMessageMK,
                            RelevanceScore = 0.88
                        });
                    }
                }
            }

            // 5. If nothing found, provide general guidance
            if (answerParts.Count == 0)
            {
                // Try a broad search across all tables
                var broadTariff = await _context.TariffCodes
                    .Where(t => t.IsActive && t.Description.Contains(question))
                    .Take(3)
                    .ToListAsync();

                if (broadTariff.Any())
                {
                    answerParts.Add("Пронајдов ги следните тарифни ознаки поврзани со вашето прашање:\n");
                    foreach (var t in broadTariff)
                    {
                        answerParts.Add($"- **{t.TariffNumber}** - {t.Description} (Царина: {t.CustomsRate}%)");
                        sources.Add(new SourceReference
                        {
                            DocumentTitle = "TARIC База",
                            Reference = t.TariffNumber,
                            ContentSnippet = t.Description,
                            RelevanceScore = 0.7
                        });
                    }
                }
                else
                {
                    answerParts.Add("Не пронајдов конкретни податоци за вашето прашање во базата на знаење.");
                    answerParts.Add("");
                    answerParts.Add("Можете да прашате за:");
                    answerParts.Add("- **Тарифни ознаки** (пр. \"Која е царинската стапка за 3901100000?\")");
                    answerParts.Add("- **Царински процедури** (пр. \"Објасни ми облагородување\")");
                    answerParts.Add("- **Кодни листи** (пр. \"Кои се процедурни кодови?\")");
                    answerParts.Add("- **Правила за валидација** (пр. \"Кои правила важат за Box 33?\")");
                    answerParts.Add("- **Регулативи** (пр. \"Регулативи за увоз на храна\")");
                }
            }

            return new RAGResponse
            {
                Success = true,
                Answer = string.Join("\n", answerParts),
                Sources = sources.Take(10).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database RAG failed for question: {Question}", question);
            return new RAGResponse
            {
                Success = false,
                ErrorMessage = "Грешка при пребарување на базата: " + ex.Message
            };
        }
    }

    public async Task<RAGResponse> ExplainConceptAsync(string concept)
    {
        return await AskQuestionAsync($"Објасни: {concept}", 10);
    }

    private async Task<List<Domain.Entities.MasterData.TariffCode>> SearchTariffCodesAsync(
        string question, string? tariffNumber, int limit)
    {
        var query = _context.TariffCodes.Where(t => t.IsActive);

        if (!string.IsNullOrEmpty(tariffNumber))
        {
            // Search by exact or prefix match on tariff number
            query = query.Where(t => t.TariffNumber.StartsWith(tariffNumber) ||
                                     t.TARBR.StartsWith(tariffNumber));
        }
        else
        {
            // Extract keywords from question for description search
            var keywords = ExtractKeywords(question);
            foreach (var kw in keywords.Take(3))
            {
                var keyword = kw; // closure
                query = query.Where(t => t.Description.Contains(keyword));
            }
        }

        return await query.Take(limit).ToListAsync();
    }

    private async Task<List<Domain.Entities.MasterData.CustomsRegulation>> SearchRegulationsAsync(
        string question, string? tariffNumber, int limit)
    {
        var query = _context.CustomsRegulations.Where(r => r.IsActive);

        if (!string.IsNullOrEmpty(tariffNumber))
        {
            query = query.Where(r => r.TariffNumber.Contains(tariffNumber));
        }
        else
        {
            var keywords = ExtractKeywords(question);
            if (keywords.Any())
            {
                var kw = keywords.First();
                query = query.Where(r =>
                    (r.DescriptionMK != null && r.DescriptionMK.Contains(kw)) ||
                    (r.DescriptionEN != null && r.DescriptionEN.Contains(kw)) ||
                    r.CelexNumber.Contains(kw) ||
                    (r.LegalBasis != null && r.LegalBasis.Contains(kw)));
            }
        }

        return await query.Take(limit).ToListAsync();
    }

    private async Task<List<Domain.Entities.MasterData.CodeListItem>> SearchCodeListsAsync(
        string question, int limit)
    {
        var q = question.ToLower();
        var query = _context.CodeListItems.Where(c => c.IsActive);

        // Try to match specific list type
        if (ContainsAny(q, "процедур", "procedure"))
            query = query.Where(c => c.ListType.Contains("Procedure") || c.ListType.Contains("процедур"));
        else if (ContainsAny(q, "документ", "document"))
            query = query.Where(c => c.ListType.Contains("Document"));
        else if (ContainsAny(q, "транспорт", "transport"))
            query = query.Where(c => c.ListType.Contains("Transport"));
        else if (ContainsAny(q, "земја", "country", "држав"))
            query = query.Where(c => c.ListType.Contains("Country"));
        else if (ContainsAny(q, "валута", "currency"))
            query = query.Where(c => c.ListType.Contains("Currency"));
        else if (ContainsAny(q, "пакување", "package"))
            query = query.Where(c => c.ListType.Contains("Package"));
        else
        {
            var keywords = ExtractKeywords(question);
            if (keywords.Any())
            {
                var kw = keywords.First();
                query = query.Where(c =>
                    c.Code.Contains(kw) ||
                    c.DescriptionMK.Contains(kw) ||
                    (c.DescriptionEN != null && c.DescriptionEN.Contains(kw)) ||
                    c.ListType.Contains(kw));
            }
        }

        return await query.OrderBy(c => c.ListType).ThenBy(c => c.SortOrder).Take(limit * 3).ToListAsync();
    }

    private async Task<List<Domain.Entities.MasterData.DeclarationRule>> SearchDeclarationRulesAsync(
        string question, int limit)
    {
        var query = _context.DeclarationRules.Where(r => r.IsActive);

        // Check for specific box number
        var boxMatch = System.Text.RegularExpressions.Regex.Match(question, @"[Bb]ox\s*(\d+\w?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (boxMatch.Success)
        {
            var boxNum = boxMatch.Groups[1].Value;
            query = query.Where(r => r.FieldName.Contains(boxNum));
        }
        else
        {
            var keywords = ExtractKeywords(question);
            if (keywords.Any())
            {
                var kw = keywords.First();
                query = query.Where(r =>
                    r.RuleCode.Contains(kw) ||
                    r.FieldName.Contains(kw) ||
                    r.ErrorMessageMK.Contains(kw) ||
                    (r.ErrorMessageEN != null && r.ErrorMessageEN.Contains(kw)));
            }
        }

        return await query.OrderBy(r => r.Priority).Take(limit).ToListAsync();
    }

    private static List<string> ExtractKeywords(string text)
    {
        // Remove common stop words and extract meaningful keywords
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "што", "е", "за", "на", "во", "со", "од", "да", "ми", "ме", "се", "не", "и", "или",
            "кои", "која", "кој", "како", "каква", "колку", "дали", "ги", "го", "ја",
            "the", "is", "for", "in", "of", "to", "a", "an", "and", "or", "what", "which", "how",
            "објасни", "кажи", "дај", "прашање", "одговор", "треба", "може", "потребни",
            "сакам", "знаеш", "обидете"
        };

        return text
            .Split(new[] { ' ', ',', '.', '?', '!', ':', ';', '-', '(', ')', '"', '\'' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct()
            .Take(5)
            .ToList();
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        return terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
    }
}
