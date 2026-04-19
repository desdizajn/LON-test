using LON.Application.Common.Importing;
using LON.Application.Common.Interfaces;
using LON.Application.Customs.Validation;
using LON.Application.Customs.Validation.Rules;
using LON.Application.KnowledgeBase.Services;
using LON.Infrastructure.Initialization;
using LON.Infrastructure.Persistence;
using LON.Infrastructure.Services;
using LON.Infrastructure.Services.Importing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LON.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        
        // Регистрирај Rule Engine
        services.AddScoped<IDeclarationRuleEngine, DeclarationRuleEngine>();
        
        // Регистрирај сите правила
        services.AddScoped<IDeclarationRule, RequiredFieldsRule>();
        services.AddScoped<IDeclarationRule, TariffCodeFormatRule>();
        services.AddScoped<IDeclarationRule, TariffCodeExistsRule>();
        services.AddScoped<IDeclarationRule, ProcedureCodeValidRule>();
        services.AddScoped<IDeclarationRule, CurrencyIsoRule>();
        services.AddScoped<IDeclarationRule, CountryIsoRule>();
        services.AddScoped<IDeclarationRule, LONAuthorizationRequiredRule>();
        services.AddScoped<IDeclarationRule, LONLineTariffWithinAuthorizationRule>();
        services.AddScoped<IDeclarationRule, SadFieldAdvisoriesRule>();
        services.AddScoped<IDeclarationRule, DutyRateLookupWarningRule>();
        services.AddScoped<IDeclarationRule, WeightSanityRule>();
        services.AddScoped<IDeclarationRule, VATRateWhitelistRule>();
        services.AddScoped<IDeclarationRule, DuplicateLineWarningRule>();
        services.AddScoped<IDeclarationRule, ExchangeRateWindowRule>();

        // Stub provider until NBRM integration lands. Swap to a real HTTP-backed
        // provider by replacing this single line.
        services.AddScoped<IExchangeRateProvider, NullExchangeRateProvider>();
        
        // Knowledge Base Services
        services.AddScoped<IDocumentChunkingService, DocumentChunkingService>();
        services.AddHttpClient("OpenAI");

        var openAiKey = configuration["OpenAI:ApiKey"];
        var enableVectorStore = configuration.GetValue<bool>("EnableVectorStore", false);

        if (!string.IsNullOrWhiteSpace(openAiKey) && enableVectorStore)
        {
            // Full RAG with OpenAI embeddings + vector search + GPT answers
            services.AddScoped<IEmbeddingService, OpenAIEmbeddingService>();
            services.AddScoped<IVectorStoreService, InMemoryVectorStoreService>();
            services.AddScoped<IRAGService, OpenAIRAGService>();
        }
        else
        {
            // Database-only RAG - search TARIC, regulations, codelists directly via SQL
            services.AddScoped<IEmbeddingService, OpenAIEmbeddingService>();
            services.AddScoped<IVectorStoreService, InMemoryVectorStoreService>();
            services.AddScoped<IRAGService, DatabaseRAGService>();
        }

        services.AddScoped<VectorStoreInitializer>();

        // Auth Service — AuthService also implements IPasswordHasher so
        // Application-layer handlers can hash passwords without referencing
        // Infrastructure (see CreateUserCommand).
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<LON.Application.Common.Interfaces.IPasswordHasher>(
            sp => sp.GetRequiredService<IAuthService>());

        // Vector Store Background Service
        services.AddHostedService<VectorStoreBackgroundService>();

        // P5.1 — generic importer: one parser per format + registry dispatcher.
        services.AddScoped<IImportFileParser, XlsxImportParser>();
        services.AddScoped<IImportFileParser, CsvImportParser>();
        services.AddScoped<IImportFileParser, TsvImportParser>();
        services.AddScoped<IImportFileParser, JsonImportParser>();
        services.AddScoped<IImportFileParser, XmlImportParser>();
        services.AddScoped<IImportFileParserRegistry, ImportFileParserRegistry>();

        return services;
    }
}
