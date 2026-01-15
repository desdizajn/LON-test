# Vector Store & RAG (Retrieval-Augmented Generation)

## Преглед

Системот поддржува **векторска база на знаење** за интелигентни одговори на прашања поврзани со царински регулативи, тарифни кодови и правилници.

## Како работи?

1. **Background Иницијализација**: Vector Store се иницијализира асинхронски во background преку `VectorStoreBackgroundService`
2. **Без блокирање**: API стартува веднаш без да чека на иницијализација
3. **Автоматско seeding**: Документи се автоматски chunk-ираат и embed-ираат со OpenAI API
4. **RAG endpoint**: `/api/knowledge-base/ask` за интелигентни одговори

## Конфигурација

### 1. Enable/Disable Vector Store

Во `appsettings.json` или `appsettings.Development.json`:

```json
{
  "EnableVectorStore": true,  // false за да се disable-ира
  "OpenAI": {
    "ApiKey": "sk-your-openai-api-key",
    "EmbeddingModel": "text-embedding-ada-002",
    "ChatModel": "gpt-4o-mini"
  }
}
```

### 2. OpenAI API Key

Земи API key од: https://platform.openai.com/api-keys

**⚠️ Важно**: Ако немаш валиден OpenAI API key, остави `EnableVectorStore: false`

### 3. Docker Environment Variables

Во `docker-compose.yml`:

```yaml
api:
  environment:
    - EnableVectorStore=true
    - OpenAI__ApiKey=sk-your-openai-api-key
```

## Background Service

### VectorStoreBackgroundService

```csharp
public class VectorStoreBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Чека 10 секунди после API startup
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        // Иницијализира векторска база во background
        await vectorStoreInitializer.InitializeAsync();
    }
}
```

**Предности**:
- ✅ API стартува брзо (< 10 секунди)
- ✅ Векторска база се иницијализира во background
- ✅ Ако е disabled, не се извршува ништо
- ✅ Грешки не го срушуваат системот

## Тестирање

### Без Vector Store (брзо)

```bash
# API стартува за < 10 секунди
docker-compose up -d
curl http://localhost:5000/health
```

### Со Vector Store (со background loading)

```bash
# 1. Постави OpenAI API key
export OPENAI_API_KEY="sk-your-key"

# 2. Enable Vector Store
# Измени appsettings.json: "EnableVectorStore": true

# 3. Rebuild и start
docker-compose up -d --build api

# 4. API е веднаш достапен
curl http://localhost:5000/health

# 5. Провери дали Vector Store е ready (после ~30-60 секунди)
docker logs lon-api | grep "Vector Store"
# Треба да видиш: "✅ Vector Store initialization completed successfully!"
```

### RAG Query Example

```bash
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r .accessToken)

curl -X POST http://localhost:5000/api/knowledge-base/ask \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Која е тарифната ознака за природна свила?",
    "language": "mk"
  }'
```

## Архитектура

```
┌─────────────────┐
│   API Startup   │
└────────┬────────┘
         │
         ├─► Миграции ✅
         ├─► Seed податоци ✅
         ├─► User Management ✅
         │
         └─► Background Service стартува
                    │
                    ├─► Чека 10 сек ⏳
                    │
                    ├─► Проверка EnableVectorStore
                    │
                    └─► Vector Store Init 📊
                           │
                           ├─► Document Seeding
                           ├─► Chunking
                           ├─► OpenAI Embeddings
                           └─► In-Memory Vector Store
```

## Troubleshooting

### Vector Store не се иницијализира

1. Провери дали е enabled:
   ```bash
   docker exec lon-api cat /app/appsettings.json | grep EnableVectorStore
   ```

2. Провери логови:
   ```bash
   docker logs lon-api | grep -i vector
   ```

### OpenAI API грешка

```
❌ Error during Vector Store initialization
System.Net.Http.HttpRequestException: Response status code does not indicate success: 401 (Unauthorized)
```

**Решение**: Постави валиден OpenAI API key

### Бавна иницијализација

- Нормално е 30-60 секунди (зависи од бројот на документи)
- API е функционален и за време на иницијализација
- RAG endpoint ќе враќа грешка додека не заврши иницијализација

## Производство (Production)

За production environment:

1. **Користи persistentно векторско решение** (Pinecone, Weaviate, Qdrant)
2. **Pre-computed embeddings** - генерирај ги offlineпред deployment
3. **Caching** - кеширај често користени embeddings
4. **Rate limiting** - ограничи OpenAI API calls
5. **Monitoring** - следи го statusот на Vector Store

## Алтернативи

### Worker Container

Можеш да ја преместиш иницијализацијата во `LON.Worker` контејнерот:

```csharp
// LON.Worker/VectorStoreWorker.cs
public class VectorStoreWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Periodic refresh на векторската база
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

### Manual Initialization

За development, можеш да ја иницијализираш рачно:

```bash
# Повикај посебен endpoint
curl -X POST http://localhost:5000/api/admin/init-vector-store \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

## Перформанси

| Операција | Време (без Vector Store) | Време (со Vector Store) |
|-----------|-------------------------|------------------------|
| API Startup | ~5 секунди | ~5 секунди (background) |
| Vector Store Ready | N/A | ~30-60 секунди |
| RAG Query | N/A | ~2-5 секунди |

## Следни чекори

- [ ] Имплементирај persistent vector store (Qdrant/Pinecone)
- [ ] Додај endpoint за manual initialization
- [ ] Имплементирај caching на embeddings
- [ ] Додај health check за Vector Store status
- [ ] Periodic refresh на векторската база
