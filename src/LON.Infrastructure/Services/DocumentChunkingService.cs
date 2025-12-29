using System.Text;
using System.Text.RegularExpressions;
using LON.Application.KnowledgeBase.Services;

namespace LON.Infrastructure.Services;

/// <summary>
/// Имплементација на document chunking service
/// </summary>
public class DocumentChunkingService : IDocumentChunkingService
{
    public List<string> ChunkDocument(string content, int maxChunkSize = 1000, int overlap = 200)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new List<string>();
        
        var chunks = new List<string>();
        var startIndex = 0;
        
        while (startIndex < content.Length)
        {
            var endIndex = Math.Min(startIndex + maxChunkSize, content.Length);
            
            // Најди последен простор/нов ред за да не сечеме зборови
            if (endIndex < content.Length)
            {
                var lastSpace = content.LastIndexOfAny(new[] { ' ', '\n', '\r', '.', '!', '?' }, endIndex, maxChunkSize);
                if (lastSpace > startIndex)
                {
                    endIndex = lastSpace + 1;
                }
            }
            
            var chunk = content.Substring(startIndex, endIndex - startIndex).Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }
            
            // Движи се напред со overlap
            startIndex = endIndex - overlap;
            if (startIndex >= content.Length) break;
        }
        
        return chunks;
    }
    
    public List<DocumentChunk> ChunkBySection(string content, string[] sectionDelimiters)
    {
        if (string.IsNullOrWhiteSpace(content) || sectionDelimiters == null || sectionDelimiters.Length == 0)
            return new List<DocumentChunk> 
            { 
                new DocumentChunk 
                { 
                    Content = content, 
                    TokenCount = EstimateTokenCount(content) 
                } 
            };
        
        var chunks = new List<DocumentChunk>();
        
        // Креирај regex pattern за сите делители (нпр. "Член 5", "Глава 3")
        var pattern = $@"(?:{'|'.Join(sectionDelimiters.Select(Regex.Escape))})\s+\d+";
        var matches = Regex.Matches(content, pattern, RegexOptions.Multiline);
        
        if (matches.Count == 0)
        {
            // Нема секции, врати целиот документ
            return new List<DocumentChunk> 
            { 
                new DocumentChunk 
                { 
                    Content = content, 
                    TokenCount = EstimateTokenCount(content) 
                } 
            };
        }
        
        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var startIndex = match.Index;
            var endIndex = (i < matches.Count - 1) ? matches[i + 1].Index : content.Length;
            
            var sectionContent = content.Substring(startIndex, endIndex - startIndex).Trim();
            var sectionTitle = match.Value;
            
            // Ако секцијата е премногу голема, дели ја дополнително
            if (sectionContent.Length > 2000)
            {
                var subChunks = ChunkDocument(sectionContent, 1500, 200);
                for (int j = 0; j < subChunks.Count; j++)
                {
                    chunks.Add(new DocumentChunk
                    {
                        Content = subChunks[j],
                        Title = $"{sectionTitle} (дел {j + 1}/{subChunks.Count})",
                        TokenCount = EstimateTokenCount(subChunks[j])
                    });
                }
            }
            else
            {
                chunks.Add(new DocumentChunk
                {
                    Content = sectionContent,
                    Title = sectionTitle,
                    TokenCount = EstimateTokenCount(sectionContent)
                });
            }
        }
        
        return chunks;
    }
    
    public int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        
        // Едноставна апроксимација: 1 token ≈ 4 карактери за кирилица
        // За поточна пресметка, треба OpenAI Tokenizer
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
