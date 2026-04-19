using FluentAssertions;
using LON.Application.Customs.Validation;
using LON.Application.Customs.Validation.Rules;
using LON.Domain.Entities.Customs;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P2.7 — in-process unit tests for the four new declaration rules. No DB,
/// no WebApplicationFactory — rules operate on plain CustomsDeclaration DTOs.
/// </summary>
public class DeclarationRuleUnitTests
{
    // ---- WeightSanityRule ----

    [Fact]
    public async Task WeightSanity_NetGreaterThanGross_FailsHard()
    {
        var rule = new WeightSanityRule();
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Lines = new List<CustomsDeclarationLine>
            {
                new() { LineNumber = 1, GrossWeight = 5m, NetWeight = 10m }
            }
        });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Message.Contains("Нето маса") && e.Message.Contains("поголема"));
    }

    [Fact]
    public async Task WeightSanity_NegativeGross_Fails()
    {
        var rule = new WeightSanityRule();
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Lines = new List<CustomsDeclarationLine>
            {
                new() { LineNumber = 1, GrossWeight = -1m }
            }
        });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task WeightSanity_ZeroWhenSet_Fails()
    {
        var rule = new WeightSanityRule();
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Lines = new List<CustomsDeclarationLine>
            {
                new() { LineNumber = 1, NetWeight = 0m }
            }
        });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task WeightSanity_BothNull_Passes()
    {
        var rule = new WeightSanityRule();
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Lines = new List<CustomsDeclarationLine>
            {
                new() { LineNumber = 1, NetWeight = null, GrossWeight = null }
            }
        });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task WeightSanity_NetEqualsGross_Passes()
    {
        var rule = new WeightSanityRule();
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Lines = new List<CustomsDeclarationLine>
            {
                new() { LineNumber = 1, GrossWeight = 5m, NetWeight = 5m }
            }
        });
        result.IsValid.Should().BeTrue();
    }

    // ---- VATRateWhitelistRule ----

    [Fact]
    public async Task VATRate_ExoticValue_EmitsWarning()
    {
        var rule = new VATRateWhitelistRule();
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Lines = new List<CustomsDeclarationLine>
            {
                new() { LineNumber = 1, VATRate = 10m }
            }
        });
        result.IsValid.Should().BeTrue("this is advisory, not blocking");
        result.Warnings.Should().ContainSingle(w => w.Message.Contains("10"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(18)]
    public async Task VATRate_StandardRates_NoWarning(decimal rate)
    {
        var rule = new VATRateWhitelistRule();
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Lines = new List<CustomsDeclarationLine>
            {
                new() { LineNumber = 1, VATRate = rate }
            }
        });
        result.Warnings.Should().BeEmpty();
    }

    // ---- DuplicateLineWarningRule ----

    [Fact]
    public async Task DuplicateLines_SameItemTariffCountry_EmitsWarning()
    {
        var itemId = Guid.NewGuid();
        var rule = new DuplicateLineWarningRule();
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Lines = new List<CustomsDeclarationLine>
            {
                new() { LineNumber = 1, ItemId = itemId, TariffCode = "2905399500", CountryOfOrigin = "DE" },
                new() { LineNumber = 2, ItemId = itemId, TariffCode = "2905399500", CountryOfOrigin = "DE" }
            }
        });
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.Message.Contains("1, 2"));
    }

    [Fact]
    public async Task DuplicateLines_DifferentCountry_NoWarning()
    {
        var itemId = Guid.NewGuid();
        var rule = new DuplicateLineWarningRule();
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Lines = new List<CustomsDeclarationLine>
            {
                new() { LineNumber = 1, ItemId = itemId, TariffCode = "2905399500", CountryOfOrigin = "DE" },
                new() { LineNumber = 2, ItemId = itemId, TariffCode = "2905399500", CountryOfOrigin = "IT" }
            }
        });
        result.Warnings.Should().BeEmpty();
    }

    // ---- ExchangeRateWindowRule ----

    private sealed class StubRateProvider : IExchangeRateProvider
    {
        private readonly decimal? _rate;
        public StubRateProvider(decimal? rate) => _rate = rate;
        public Task<decimal?> GetRateAsync(string currency, DateTime date, CancellationToken cancellationToken = default)
            => Task.FromResult(_rate);
    }

    [Fact]
    public async Task ExchangeRate_WithinTolerance_Passes()
    {
        // NBRM: 1 EUR ≈ 61.50 MKD; declared 62.00 → ~0.8% off, inside ±20%.
        var rule = new ExchangeRateWindowRule(new StubRateProvider(61.5m));
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Currency = "EUR",
            ExchangeRate = 62.00m,
            DeclarationDate = DateTime.UtcNow.Date
        });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExchangeRate_25PercentOff_Fails()
    {
        var rule = new ExchangeRateWindowRule(new StubRateProvider(60m));
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Currency = "EUR",
            ExchangeRate = 80m,  // 33% off
            DeclarationDate = DateTime.UtcNow.Date
        });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("НБРМ"));
    }

    [Fact]
    public async Task ExchangeRate_ProviderReturnsNull_Skips()
    {
        var rule = new ExchangeRateWindowRule(new StubRateProvider(null));
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Currency = "USD",
            ExchangeRate = 55m,
            DeclarationDate = DateTime.UtcNow.Date
        });
        result.IsValid.Should().BeTrue("silent skip when provider has no rate");
    }

    [Fact]
    public async Task ExchangeRate_MKDDeclaration_Skips()
    {
        var rule = new ExchangeRateWindowRule(new StubRateProvider(1m));
        var result = await rule.ValidateAsync(new CustomsDeclaration
        {
            Currency = "MKD",
            ExchangeRate = 999m,  // nonsense — but ignored because MKD
            DeclarationDate = DateTime.UtcNow.Date
        });
        result.IsValid.Should().BeTrue();
    }
}
