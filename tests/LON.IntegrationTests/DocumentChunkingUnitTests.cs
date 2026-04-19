using FluentAssertions;
using LON.Infrastructure.Services;
using Xunit;

namespace LON.IntegrationTests;

/// <summary>
/// P6.14 regression guard — <see cref="DocumentChunkingService.ChunkDocument"/>
/// used to hit an infinite loop (→ <c>OutOfMemoryException</c>) whenever
/// <c>endIndex</c> clamped to <c>content.Length</c> but the next
/// <c>startIndex = endIndex − overlap</c> stalled at or before the previous
/// <c>startIndex</c>. This surfaced on VPS when seeding the ~120 KB Pravilnik
/// document and crashed <c>VectorStoreBackgroundService</c> on every startup.
/// </summary>
public class DocumentChunkingUnitTests
{
    [Fact]
    public void ChunkDocument_StopsAtEndOfContent_EvenWhenOverlapWouldStall()
    {
        var svc = new DocumentChunkingService();
        // Slightly longer than maxChunkSize so the last chunk clamps to content.Length.
        var content = new string('a', 1050);

        var chunks = svc.ChunkDocument(content, maxChunkSize: 1000, overlap: 200);

        chunks.Should().NotBeEmpty();
        chunks.Count.Should().BeLessThan(10, "a 1 050-char document must chunk to a handful of pieces, not loop");
        string.Join(string.Empty, chunks).Length.Should().BeGreaterOrEqualTo(content.Length);
    }

    [Fact]
    public void ChunkDocument_LongRealisticDocument_ProducesLinearNumberOfChunks()
    {
        var svc = new DocumentChunkingService();
        // Simulate the Pravilnik regex-tokenised paragraph stream: 120 KB of
        // Cyrillic-ish text with natural word breaks.
        var chunk = "Член 5 од Правилникот опфаќа царинска постапка за облагородување. ";
        var content = string.Concat(Enumerable.Repeat(chunk, 1800)); // ~120 KB

        var chunks = svc.ChunkDocument(content, maxChunkSize: 1000, overlap: 200);

        // maxChunkSize=1000, overlap=200 → ~800 net per iteration → ~content.Length/800
        var expectedCeiling = (content.Length / 500) + 5;
        chunks.Count.Should().BeLessThan(expectedCeiling, "chunking must not explode; each iteration consumes at least maxChunkSize−overlap characters");
    }

    [Fact]
    public void ChunkDocument_EmptyInput_ReturnsEmpty()
    {
        var svc = new DocumentChunkingService();
        svc.ChunkDocument("").Should().BeEmpty();
        svc.ChunkDocument("   ").Should().BeEmpty();
    }

    [Fact]
    public void ChunkDocument_ShorterThanMaxSize_ReturnsSingleChunk()
    {
        var svc = new DocumentChunkingService();
        var content = "Just a short line.";
        var chunks = svc.ChunkDocument(content, maxChunkSize: 1000, overlap: 200);
        chunks.Should().ContainSingle().Which.Should().Be(content);
    }
}
