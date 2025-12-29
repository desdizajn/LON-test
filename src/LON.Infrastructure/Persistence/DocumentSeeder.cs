using LON.Application.Common.Interfaces;
using LON.Application.KnowledgeBase.Services;
using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LON.Infrastructure.Persistence;

/// <summary>
/// Seeder за Knowledge Base документи (Правилник, Упатства...)
/// </summary>
public static class DocumentSeeder
{
    public static async Task SeedDocumentsAsync(ApplicationDbContext context, IDocumentChunkingService chunkingService, IEmbeddingService embeddingService)
    {
        Console.WriteLine("🔄 Starting Document seeding...");
        
        // Провери дали веќе има документи
        if (await context.KnowledgeDocuments.AnyAsync())
        {
            Console.WriteLine("✅ Documents already seeded. Skipping.");
            return;
        }

        // Seed Правилник за примена на царинска тарифа
        await SeedPravilnikAsync(context, chunkingService, embeddingService);
        
        // Seed SADка Упатство
        await SeedSADInstructionsAsync(context, chunkingService, embeddingService);
        
        Console.WriteLine("✅ Document seeding completed!");
    }

    private static async Task SeedPravilnikAsync(ApplicationDbContext context, IDocumentChunkingService chunkingService, IEmbeddingService embeddingService)
    {
        Console.WriteLine("📄 Seeding Правилник за примена на царинска тарифа...");
        
        // Пример документи од Правилник (би требало да се читаат од реални фајлови)
        var pravilnikSections = new[]
        {
            new { Reference = "Член 1", Content = "Овој правилник ги пропишува условите и начинот на примена на царинската тарифа согласно Законот за царинска тарифа. Царинската тарифа се применува при увоз, извоз и транзит на стоки низ царинската територија на Република Северна Македонија." },
            new { Reference = "Член 5", Content = "Тарифната ознака се определува врз основа на физичките карактеристики на стоката, нејзината хемиска состојка, намената и степенот на обработка. При класификација на стоките се применуваат Основните правила за толкување на Хармонизираната номенклатура." },
            new { Reference = "Глава 1", Content = "Општи одредби за примена на номенклатурата. Хармонизираната номенклатура содржи 21 поглавје кои ги опфаќаат сите видови на стоки кои можат да бидат предмет на царинење." },
            new { Reference = "Глава 50", Content = "Свила - класификација на природна свила, влакна од свила, предива и ткаенини. Тарифните ознаки од 5001 до 5007 ги опфаќаат сите производи од свила и нејзините дериват." }
        };

        foreach (var section in pravilnikSections)
        {
            var document = new KnowledgeDocument
            {
                Id = Guid.NewGuid(),
                DocumentType = "Правилник",
                TitleMK = "Правилник за примена на царинска тарифа",
                TitleEN = "Regulation on Application of Customs Tariff",
                Reference = section.Reference,
                Content = section.Content,
                Language = "MK",
                SourceUrl = "https://customs.gov.mk/regulations/pravilnik",
                Version = "2024",
                DocumentDate = new DateTime(2024, 1, 1),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "DocumentSeeder"
            };

            // Chunk документот
            var chunks = chunkingService.ChunkDocument(
                document.Content,
                maxChunkSize: 500,
                overlap: 50);

            document.Chunks = new List<KnowledgeDocumentChunk>();
            
            for (int i = 0; i < chunks.Count; i++)
            {
                var embedding = await embeddingService.GenerateEmbeddingAsync(chunks[i]);
                var embeddingJson = System.Text.Json.JsonSerializer.Serialize(embedding);
                
                document.Chunks.Add(new KnowledgeDocumentChunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    ChunkIndex = i,
                    Content = chunks[i],
                    TokenCount = chunkingService.EstimateTokenCount(chunks[i]),
                    Embedding = embeddingJson,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "DocumentSeeder"
                });
            }

            await context.KnowledgeDocuments.AddAsync(document);
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ Seeded {pravilnikSections.Length} sections from Правилник");
    }

    private static async Task SeedSADInstructionsAsync(ApplicationDbContext context, IDocumentChunkingService chunkingService, IEmbeddingService embeddingService)
    {
        Console.WriteLine("📄 Seeding SADка упатства...");
        
        // Пример упатства за Box полиња
        var sadInstructions = new[]
        {
            new { BoxNumber = "Box 01", Content = "Box 01 - Декларација: Внесете го кодот на видот на декларација. Првите две цифри означуваат режим (IM=увоз, EX=извоз, CO=заедничка транзит), третата цифра го означува видот на декларација (A=нормална, B=поедноставена, C=допрецизирање, D=заеднички транзит)." },
            new { BoxNumber = "Box 02", Content = "Box 02 - Испраќач/Извозник: Целосно име и адреса на економскиот оператор кој ја испраќа стоката. Ако е регистриран во системот EORI, треба да се внесе и EORI бројот." },
            new { BoxNumber = "Box 33", Content = "Box 33 - Тарифна ознака: Внесете ја 10-цифрената тарифна ознака согласно Хармонизираната номенклатура. Првите 6 цифри се HS кодот, следните 2 се CN кодот (ЕУ), и последните 2 се националниот код (TARIC)." },
            new { BoxNumber = "Box 37", Content = "Box 37 - Режим: Внесете го кодот на бараниот царински режим. Примери: 4000 = Ставање во слободен промет, 5100 = Активно племенување, 5351 = Привремен увоз со целосно ослободување." },
            new { BoxNumber = "Box 47", Content = "Box 47 - Пресметка на давачките: Овде се внесуваат податоците за пресметка на царина, ДДВ и други давачки. За секој вид давачка се наведува основата за пресметка, стапката и износот." }
        };

        foreach (var instruction in sadInstructions)
        {
            var document = new KnowledgeDocument
            {
                Id = Guid.NewGuid(),
                DocumentType = "SADка Упатство",
                TitleMK = $"Упатство за пополнување на {instruction.BoxNumber}",
                TitleEN = $"Instructions for filling {instruction.BoxNumber}",
                Reference = instruction.BoxNumber,
                Content = instruction.Content,
                Language = "MK",
                SourceUrl = "https://customs.gov.mk/instructions/sad",
                Version = "2024",
                DocumentDate = new DateTime(2024, 1, 1),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "DocumentSeeder"
            };

            // За кратки упатства, нема потреба од chunking - еден chunk е доволен
            var embedding = await embeddingService.GenerateEmbeddingAsync(document.Content);
            var embeddingJson = System.Text.Json.JsonSerializer.Serialize(embedding);
            
            document.Chunks = new List<KnowledgeDocumentChunk>
            {
                new KnowledgeDocumentChunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    ChunkIndex = 0,
                    Content = document.Content,
                    TokenCount = document.Content.Split(' ').Length,
                    Embedding = embeddingJson,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "DocumentSeeder"
                }
            };

            await context.KnowledgeDocuments.AddAsync(document);
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ Seeded {sadInstructions.Length} SADка упатства");
    }
}
