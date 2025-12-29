# Phase 3: RAG (Retrieval-Augmented Generation) - ЗАВРШЕНО ✅

## 📅 Датум: 29 Декември 2024

## 🎯 Цел на Фаза 3

Имплементација на **Vector Store + RAG** систем за интелигентно пребарување и генерирање одговори врз база на царински документи, правилници и упатства.

---

## ✅ Имплементирано

### 1. **Domain Layer**

#### 1.1 Entities
- ✅ **KnowledgeDocument** - Царински документ (Правилник, Упатство, SADка)
  - `DocumentType`, `TitleMK`, `TitleEN`, `Reference`, `Content`
  - `Language`, `SourceUrl`, `Version`, `DocumentDate`
  - Navigation property: `Chunks`

- ✅ **KnowledgeDocumentChunk** - Chunk (парче) од документ за vector search
  - `DocumentId`, `ChunkIndex`, `Content`, `ChunkTitle`
  - `Embedding` (JSON serialized float array)
  - `TokenCount`, `Metadata`

#### 1.2 Database Migrations
- ✅ Migration: `AddDocumentVectorStore`
- ✅ Tables: `KnowledgeDocuments`, `KnowledgeDocumentChunks`
- ✅ EF Core Configurations

---

### 2. **Application Layer**

#### 2.1 Services (Interfaces)

**IDocumentChunkingService**
```csharp
List<string> ChunkDocument(string content, int maxChunkSize = 1000, int overlap = 200);
List<DocumentChunk> ChunkBySection(string content, string[] sectionDelimiters);
int EstimateTokenCount(string text);
```

**IEmbeddingService**
```csharp
Task<float[]> GenerateEmbeddingAsync(string text);
Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts);
double CosineSimilarity(float[] a, float[] b);
```

**IVectorStoreService**
```csharp
Task IndexDocumentAsync(KnowledgeDocument document, List<KnowledgeDocumentChunk> chunks);
Task<List<SearchResult>> SearchAsync(string query, int topK = 5, double minSimilarity = 0.7);
Task<List<SearchResult>> SearchByDocumentTypeAsync(string query, string documentType, int topK = 5);
```

**IRAGService**
```csharp
Task<RAGResponse> AskQuestionAsync(string question, int maxContextChunks = 3);
Task<RAGResponse> ExplainConceptAsync(string concept);
```

#### 2.2 DTOs
- ✅ `RAGResponse` - Одговор со извори и references
- ✅ `SourceReference` - Референца кон документ
- ✅ `SearchResult` - Резултат од vector search
- ✅ `DocumentChunk` - Chunk со контекст

---

### 3. **Infrastructure Layer**

#### 3.1 Service Implementations

**DocumentChunkingService**
- ✅ Chunking by character length со overlap
- ✅ Chunking by section (Член, Глава, Став)
- ✅ Token count estimation (approx 1 token ≈ 4 chars)

**OpenAIEmbeddingService**
- ✅ Integration со OpenAI `text-embedding-ada-002`
- ✅ Batch embeddings generation
- ✅ Cosine similarity calculation
- ✅ Error handling и retry logic

**InMemoryVectorStoreService**
- ✅ In-memory vector store (може да се замени со Qdrant/Chroma/pgvector)
- ✅ Semantic search со cosine similarity
- ✅ Filtered search по DocumentType
- ✅ Индексирање на документи и chunks

**OpenAIRAGService**
- ✅ RAG pipeline: Retrieve + Generate
- ✅ Integration со OpenAI GPT-4o-mini
- ✅ Context-aware prompt engineering
- ✅ Source citation и reference tracking
- ✅ Македонски јазик поддршка

#### 3.2 Data Initialization

**DocumentSeeder**
- ✅ Seeding на Правилник samples (Член 1, Член 5, Глава 1, Глава 50)
- ✅ Seeding на SADка упатства (Box 01, Box 02, Box 33, Box 37, Box 47)
- ✅ Автоматско chunking и embedding generation
- ✅ Database persistence

**VectorStoreInitializer**
- ✅ Иницијализација на Vector Store при startup
- ✅ Loading на documents и chunks
- ✅ Logging и error handling

---

### 4. **API Layer**

#### 4.1 KnowledgeBaseController Endpoints

✅ **POST /api/KnowledgeBase/ask**
- Постави прашање за царински регулативи/процедури
- Request: `{ "question": "...", "maxContextChunks": 3 }`
- Response: `RAGResponse` со одговор и извори

✅ **POST /api/KnowledgeBase/explain**
- Побарај објаснување за концепт
- Request: `{ "concept": "Box 33" }`
- Response: `RAGResponse` со објаснување

✅ **POST /api/KnowledgeBase/search**
- Semantic search во Knowledge Base
- Request: `{ "query": "...", "topK": 5, "minSimilarity": 0.7, "documentType": "Правилник" }`
- Response: `List<SearchResult>`

✅ **GET /api/KnowledgeBase/health**
- Health check - провери дали Vector Store е иницијализиран
- Response: `{ "status": "Healthy", "hasDocuments": true }`

✅ **GET /api/KnowledgeBase/stats**
- Статистики за Knowledge Base
- Response: `{ "totalDocuments": 9, "totalChunks": 15, "embeddingCoverage": 100 }`

---

## 🏗️ Архитектура

```
┌─────────────────────────────────────────────────────────────┐
│                      Client Application                      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   KnowledgeBaseController                    │
│  /ask  │  /explain  │  /search  │  /health  │  /stats      │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌─────────────┐    ┌──────────────────┐    ┌─────────────────┐
│ RAGService  │    │ VectorStoreService│    │ EmbeddingService│
└─────────────┘    └──────────────────┘    └─────────────────┘
        │                     │                     │
        └─────────────────────┼─────────────────────┘
                              ▼
                    ┌──────────────────┐
                    │  OpenAI API      │
                    │ (GPT + Embeddings)│
                    └──────────────────┘
                              │
                              ▼
                ┌──────────────────────────┐
                │   SQL Server Database    │
                │ KnowledgeDocuments       │
                │ KnowledgeDocumentChunks  │
                └──────────────────────────┘
```

---

## 🔄 RAG Pipeline Flow

```
User Question
    │
    ▼
┌─────────────────────────────────────┐
│ 1. Generate Query Embedding         │
│    OpenAI text-embedding-ada-002    │
└─────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────┐
│ 2. Vector Search                    │
│    Cosine Similarity > minThreshold │
│    Return Top K chunks              │
└─────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────┐
│ 3. Build Context Prompt             │
│    [Извор 1: ...] + [Извор 2: ...]  │
└─────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────┐
│ 4. Generate Answer                  │
│    GPT-4o-mini со контекст          │
│    Македонски јазик                 │
└─────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────┐
│ 5. Return Response                  │
│    Answer + Source References       │
└─────────────────────────────────────┘
```

---

## 🧪 Testing

### Test Script: `test-rag.sh`

Автоматизирани тестови за:
1. ✅ Health Check
2. ✅ Statistics
3. ✅ Semantic Search (Општо)
4. ✅ Semantic Search (Filtered by DocumentType)
5. ✅ RAG Question Answering
6. ✅ RAG Concept Explanation
7. ✅ Complex Questions

### Како да се тестира:

```bash
# 1. Стартувај API
cd /workspaces/LON-test
docker-compose up -d sqlserver
dotnet run --project src/LON.API/LON.API.csproj

# 2. Пушти тестови (во друг терминал)
chmod +x test-rag.sh
./test-rag.sh
```

---

## 📊 Seeded Data

### Правилник (4 документи)
1. **Член 1** - Општи одредби за примена
2. **Член 5** - Тарифна ознака и класификација
3. **Глава 1** - Општи одредби за номенклатура
4. **Глава 50** - Свила - класификација

### SADка Упатства (5 документи)
1. **Box 01** - Декларација (IM/EX/CO кодови)
2. **Box 02** - Испраќач/Извозник (EORI број)
3. **Box 33** - Тарифна ознака (10-digit HS/CN/TARIC)
4. **Box 37** - Режим (Царински режим кодови)
5. **Box 47** - Пресметка на давачки (Царина, ДДВ)

---

## 🔧 Configuration

### appsettings.json

```json
{
  "OpenAI": {
    "ApiKey": "your-openai-api-key",
    "EmbeddingModel": "text-embedding-ada-002",
    "ChatModel": "gpt-4o-mini",
    "MaxTokens": 1000,
    "Temperature": 0.7
  },
  "VectorStore": {
    "Type": "InMemory",
    "MinSimilarityThreshold": 0.7,
    "DefaultTopK": 5
  }
}
```

### Environment Variables

```bash
OpenAI__ApiKey=sk-...
ConnectionStrings__DefaultConnection=Server=...
```

---

## 🚀 Deployment Considerations

### Production Improvements

1. **Vector Database**
   - Замени In-Memory со **Qdrant** или **Pinecone** или **pgvector**
   - Подобро performance за голем број на документи
   - Персистентен storage

2. **Embeddings Cache**
   - Cache на embeddings за често користени queries
   - Redis cache layer

3. **Document Processing**
   - Batch processing на документи
   - Background jobs за chunking и embedding
   - PDF/DOCX parsing

4. **Monitoring**
   - OpenTelemetry за tracing
   - Cost tracking за OpenAI API
   - Performance metrics

5. **Security**
   - API Key rotation
   - Rate limiting
   - Content filtering

---

## 📈 Next Steps (Phase 4)

- [ ] **Real Document Import**
  - PDF parsing (Правилник, Упатства)
  - OCR за скенирани документи
  - Metadata extraction

- [ ] **Advanced Chunking**
  - Semantic chunking (spaCy/sentence-transformers)
  - Chunk overlap strategy optimization
  - Table/image extraction

- [ ] **Multi-lingual Support**
  - EN/MK parallel documents
  - Cross-lingual search

- [ ] **Query Understanding**
  - Intent classification
  - Entity extraction (Box numbers, Article references)
  - Query expansion

- [ ] **User Feedback Loop**
  - Rating на одговори (👍/👎)
  - Reinforcement learning from feedback
  - A/B testing на prompt strategies

---

## 🎉 Заклучок

**Phase 3 е успешно завршена!** 

Системот сега има:
- ✅ Vector Store infrastructure
- ✅ Document chunking
- ✅ Embedding generation (OpenAI)
- ✅ Semantic search
- ✅ RAG pipeline (Retrieval + Generation)
- ✅ API endpoints за testing
- ✅ Sample data (Правилник + SADка упатства)

**Ready for production-level enhancements and real document integration!**
