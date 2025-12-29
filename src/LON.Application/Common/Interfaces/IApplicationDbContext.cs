using LON.Domain.Entities.Customs;
using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Master Data - Knowledge Base
    DbSet<TariffCode> TariffCodes { get; }
    DbSet<CustomsRegulation> CustomsRegulations { get; }
    DbSet<DeclarationRule> DeclarationRules { get; }
    DbSet<CodeListItem> CodeListItems { get; }
    DbSet<KnowledgeDocument> KnowledgeDocuments { get; }
    DbSet<KnowledgeDocumentChunk> KnowledgeDocumentChunks { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
