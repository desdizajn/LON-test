using LON.Application.Common.Interfaces;
using LON.Application.Customs.Validation;
using LON.Application.Customs.Validation.Rules;
using LON.Application.KnowledgeBase.Services;
using LON.Infrastructure.Persistence;
using LON.Infrastructure.Services;
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
        
        // Knowledge Base Services (Phase 3: RAG)
        services.AddScoped<IDocumentChunkingService, DocumentChunkingService>();
        services.AddScoped<IEmbeddingService, OpenAIEmbeddingService>();
        services.AddScoped<IVectorStoreService, InMemoryVectorStoreService>();
        services.AddScoped<IRAGService, OpenAIRAGService>();
        
        // HttpClient за OpenAI
        services.AddHttpClient("OpenAI");

        return services;
    }
}
