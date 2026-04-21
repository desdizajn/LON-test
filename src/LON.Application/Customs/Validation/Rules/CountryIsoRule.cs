using LON.Domain.Entities.Customs;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Правило: Box 15 (CountryOfDispatch), Box 17 (CountryOfDestination) и
/// секоја Line.CountryOfOrigin (Box 34) мораат да бидат валидни ISO 3166-1
/// alpha-2 кодови. Листата е ограничена на 50 земји од листата
/// kb/processed/countries_box15a_iso.json.
/// </summary>
public class CountryIsoRule : IDeclarationRule
{
    public string RuleCode => "BOX15_17_34_COUNTRY_ISO";
    public int Priority => 16;

    /// <summary>
    /// ISO 3166-1 alpha-2 кодови за земји, листа согласно Правилник
    /// (kb/processed/countries_box15a_iso.json). Overinclusive is safer
    /// than underinclusive — a rejected-by-KB country will still hit the
    /// customs backend and fail there with a better error.
    /// </summary>
    private static readonly HashSet<string> AllowedCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "MK","AL","AT","AU","BA","BE","BG","BR","CA","CH","CN","CY","CZ","DE",
        "DK","EE","ES","FI","FR","GB","GR","HR","HU","IE","IL","IN","IT","JP",
        "KR","LT","LU","LV","ME","MT","NL","NO","PL","PT","RO","RS","RU","SE",
        "SI","SK","TR","UA","US","XK","VN","VA"
    };

    public Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        const string fieldName = "Box15_17_34_CountryCode";
        var invalid = new List<string>();

        void Check(string? code, string box)
        {
            if (!string.IsNullOrWhiteSpace(code) && !AllowedCountries.Contains(code))
                invalid.Add($"{box} '{code}'");
        }

        Check(declaration.CountryOfDispatch, "Рубрика 15 CountryOfDispatch");
        Check(declaration.CountryOfDestination, "Рубрика 17 CountryOfDestination");
        Check(declaration.SenderCountry, "Рубрика 02 SenderCountry");

        foreach (var line in declaration.Lines)
        {
            Check(line.CountryOfOrigin, $"Линија {line.LineNumber} Рубрика 34 CountryOfOrigin");
        }

        if (invalid.Count > 0)
        {
            return Task.FromResult(ValidationRuleResult.Failure(
                RuleCode, fieldName,
                $"Невалидни ISO 3166 кодови: {string.Join(", ", invalid)}.",
                "Правилник, Прилог — listType=Country"));
        }

        return Task.FromResult(ValidationRuleResult.Success(RuleCode, fieldName));
    }
}
