# Knowledge Base API - Testing Examples

Ова е set на примери за тестирање на RAG функционалноста.

## Prerequisites

```bash
# Стартувај системот
docker-compose up -d
dotnet run --project src/LON.API/LON.API.csproj

# Чекај 30 секунди за иницијализација на Vector Store
```

## 1. Health Check

Провери дали Vector Store е активен и има документи.

```bash
curl -X GET http://localhost:5000/api/KnowledgeBase/health | jq
```

**Очекуван резултат:**
```json
{
  "status": "Healthy",
  "message": "Vector Store е активен и содржи документи",
  "hasDocuments": true,
  "timestamp": "2024-12-29T10:00:00Z"
}
```

---

## 2. Statistics

Статистики за Knowledge Base - број на документи, chunks, embeddings.

```bash
curl -X GET http://localhost:5000/api/KnowledgeBase/stats | jq
```

**Очекуван резултат:**
```json
{
  "totalDocuments": 9,
  "totalChunks": 15,
  "documentsWithEmbeddings": 15,
  "embeddingCoverage": 100,
  "timestamp": "2024-12-29T10:00:00Z"
}
```

---

## 3. Semantic Search - Општо пребарување

Пребарување низ сите документи без филтер.

```bash
curl -X POST http://localhost:5000/api/KnowledgeBase/search \
  -H "Content-Type: application/json" \
  -d '{
    "query": "тарифна ознака и класификација на стоки",
    "topK": 3,
    "minSimilarity": 0.5
  }' | jq
```

**Очекуван резултат:**
```json
[
  {
    "chunkId": "...",
    "documentId": "...",
    "content": "Тарифната ознака се определува врз основа...",
    "chunkTitle": null,
    "documentTitle": "Правилник за примена на царинска тарифа",
    "documentType": "Правилник",
    "reference": "Член 5",
    "similarityScore": 0.85
  },
  ...
]
```

---

## 4. Semantic Search - Филтрирано по тип

Пребарување само во Правилник документи.

```bash
curl -X POST http://localhost:5000/api/KnowledgeBase/search \
  -H "Content-Type: application/json" \
  -d '{
    "query": "Хармонизирана номенклатура",
    "topK": 2,
    "minSimilarity": 0.6,
    "documentType": "Правилник"
  }' | jq
```

---

## 5. Semantic Search - SADка упатства

Пребарување само во SADка упатства.

```bash
curl -X POST http://localhost:5000/api/KnowledgeBase/search \
  -H "Content-Type: application/json" \
  -d '{
    "query": "Box 33 тарифна ознака 10 цифри",
    "topK": 2,
    "documentType": "SADка Упатство"
  }' | jq
```

**Очекуван резултат:**
```json
[
  {
    "documentTitle": "Упатство за пополнување на Box 33",
    "documentType": "SADка Упатство",
    "reference": "Box 33",
    "content": "Box 33 - Тарифна ознака: Внесете ја 10-цифрената тарифна ознака...",
    "similarityScore": 0.91
  }
]
```

---

## 6. RAG - Ask Question (Едноставно прашање)

Постави прашање и добиј одговор со извори.

```bash
curl -X POST http://localhost:5000/api/KnowledgeBase/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Како се пополнува Box 33?",
    "maxContextChunks": 3
  }' | jq
```

**Очекуван резултат:**
```json
{
  "answer": "Box 33 е полето за тарифна ознака во царинската декларација. Треба да се внесе 10-цифрената тарифна ознака согласно Хармонизираната номенклатура. Првите 6 цифри се HS кодот...",
  "sources": [
    {
      "documentTitle": "Упатство за пополнување на Box 33",
      "reference": "Box 33",
      "contentSnippet": "Box 33 - Тарифна ознака: Внесете ја 10-цифрената...",
      "relevanceScore": 0.91
    }
  ],
  "errorMessage": null,
  "success": true
}
```

---

## 7. RAG - Explain Concept

Објасни царински концепт или термин.

```bash
curl -X POST http://localhost:5000/api/KnowledgeBase/explain \
  -H "Content-Type: application/json" \
  -d '{
    "concept": "Тарифна ознака"
  }' | jq
```

**Очекуван резултат:**
```json
{
  "answer": "Тарифната ознака е 10-цифрен код кој се користи за класификација на стоки при царинење. Таа се состои од: - Првите 6 цифри: HS код (Хармонизирана номенклатура) - Следните 2 цифри: CN код (Комбинирана номенклатура на ЕУ) - Последните 2 цифри: Национален TARIC код...",
  "sources": [...],
  "success": true
}
```

---

## 8. RAG - Complex Question

Комплексно прашање кое бара повеќе контекст.

```bash
curl -X POST http://localhost:5000/api/KnowledgeBase/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Што е Хармонизирана номенклатура и како се користи при царинење?",
    "maxContextChunks": 5
  }' | jq
```

---

## 9. RAG - Procedure Question

Прашање за процедура или упатство.

```bash
curl -X POST http://localhost:5000/api/KnowledgeBase/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Што содржи Box 02 и кои податоци треба да се внесат?",
    "maxContextChunks": 2
  }' | jq
```

**Очекуван резултат:**
```json
{
  "answer": "Box 02 е полето за Испраќач/Извозник. Во ова поле треба да се внесе целосното име и адреса на економскиот оператор кој ја испраќа стоката. Ако операторот е регистриран во системот EORI, треба да се внесе и неговиот EORI број. [Извор 1]",
  "sources": [
    {
      "documentTitle": "Упатство за пополнување на Box 02",
      "reference": "Box 02",
      "relevanceScore": 0.93
    }
  ],
  "success": true
}
```

---

## 10. RAG - Multi-Box Question

Прашање кое спомнува повеќе Box-ови.

```bash
curl -X POST http://localhost:5000/api/KnowledgeBase/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Каква е разликата помеѓу Box 33 и Box 37?",
    "maxContextChunks": 4
  }' | jq
```

---

## 11. Error Handling - Empty Question

Тест на error handling за празно прашање.

```bash
curl -X POST http://localhost:5000/api/KnowledgeBase/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "",
    "maxContextChunks": 3
  }' | jq
```

**Очекуван резултат:**
```json
{
  "answer": "",
  "sources": [],
  "errorMessage": "Прашањето не може да биде празно",
  "success": false
}
```

---

## 12. Search with Low Similarity Threshold

Тест со многу низок threshold - повеќе резултати.

```bash
curl -X POST http://localhost:5000/api/KnowledgeBase/search \
  -H "Content-Type: application/json" \
  -d '{
    "query": "царина",
    "topK": 10,
    "minSimilarity": 0.3
  }' | jq
```

---

## Automated Testing Script

За автоматизирано тестирање, користи:

```bash
./test-rag.sh
```

Оваа скрипта извршува сите горенаведени тестови и дава детален извештај.

---

## Performance Considerations

### Response Times (Очекувани)

- **Health Check**: ~10-50ms
- **Statistics**: ~50-100ms
- **Semantic Search**: ~200-500ms (зависи од број на chunks)
- **RAG Question**: ~2-5 секунди (OpenAI API call)

### Optimization Tips

1. **Cache embeddings** за често користени queries
2. **Batch processing** за генерирање на embeddings
3. **Index optimization** за vector search
4. **Connection pooling** за database и HTTP клиенти

---

## Troubleshooting

### Vector Store не е иницијализиран

**Симптом**: Health check враќа `hasDocuments: false`

**Решение**:
```bash
# Провери логови при startup
docker logs <container-id>

# Рестартувај API
docker-compose restart api
```

### OpenAI API грешки

**Симптом**: RAG прашања враќаат error

**Решение**:
```bash
# Провери API key
echo $OPENAI_API_KEY

# Провери appsettings.json
cat src/LON.API/appsettings.json | grep OpenAI

# Провери rate limits на OpenAI dashboard
```

### Semantic Search не наоѓа резултати

**Симптом**: Search враќа празен array

**Решение**:
1. Намали `minSimilarity` threshold (0.3-0.5)
2. Провери дали embeddings се генерирани:
```bash
curl http://localhost:5000/api/KnowledgeBase/stats
```
3. Користи поопшти термини во query

---

## Next Steps

- Додај повеќе документи (реални PDF-ови од Правилник)
- Имплементирај feedback loop (👍/👎 rating)
- Додај entity extraction (Box numbers, Article references)
- Додај query understanding и intent classification
