# 🎯 Phase 3 - Quick Start Guide

## Брз старт за тестирање на RAG функционалност

### 1️⃣ Стартувај системот

```bash
cd /workspaces/LON-test

# Стартувај SQL Server
docker-compose up -d sqlserver

# Чекај 10 секунди за SQL Server да се подигне
sleep 10

# Стартувај API
dotnet run --project src/LON.API/LON.API.csproj
```

**Важно**: Чекај ~30 секунди за:
- Database migrations
- Document seeding
- Vector store initialization
- Embeddings generation

Кога видиш:
```
✅ Vector Store initialized with X chunks
Now listening on: http://localhost:5000
```
...тогаш системот е ready!

---

### 2️⃣ Провери дали работи

```bash
# Health check
curl http://localhost:5000/api/KnowledgeBase/health | jq

# Треба да видиш:
# { "status": "Healthy", "hasDocuments": true }
```

---

### 3️⃣ Тестирај RAG

```bash
# Едноставно прашање
curl -X POST http://localhost:5000/api/KnowledgeBase/ask \
  -H "Content-Type: application/json" \
  -d '{"question": "Како се пополнува Box 33?"}' | jq
```

**Очекуван одговор:**
```json
{
  "answer": "Box 33 е полето за тарифна ознака. Треба да се внесе 10-цифрената тарифна ознака согласно Хармонизираната номенклатура...",
  "sources": [
    {
      "documentTitle": "Упатство за пополнување на Box 33",
      "reference": "Box 33",
      "relevanceScore": 0.91
    }
  ],
  "success": true
}
```

---

### 4️⃣ Пушти сите тестови

```bash
./test-rag.sh
```

Оваа скрипта автоматски тестира:
- ✅ Health Check
- ✅ Statistics
- ✅ Semantic Search
- ✅ RAG Questions
- ✅ Concept Explanation

---

## 📝 Дополнителни примери

### Semantic Search

```bash
curl -X POST http://localhost:5000/api/KnowledgeBase/search \
  -H "Content-Type: application/json" \
  -d '{
    "query": "тарифна ознака и класификација",
    "topK": 3,
    "minSimilarity": 0.5
  }' | jq
```

### Објасни концепт

```bash
curl -X POST http://localhost:5000/api/KnowledgeBase/explain \
  -H "Content-Type: application/json" \
  -d '{"concept": "Тарифна ознака"}' | jq
```

### Статистики

```bash
curl http://localhost:5000/api/KnowledgeBase/stats | jq
```

---

## 🔧 Configuration

**ВАЖНО**: За да работи RAG, потребен е OpenAI API key!

### Опција 1: Environment Variable

```bash
export OPENAI_API_KEY="sk-your-api-key-here"
dotnet run --project src/LON.API/LON.API.csproj
```

### Опција 2: appsettings.json

```json
{
  "OpenAI": {
    "ApiKey": "sk-your-api-key-here",
    "EmbeddingModel": "text-embedding-ada-002",
    "ChatModel": "gpt-4o-mini",
    "MaxTokens": 1000,
    "Temperature": 0.7
  }
}
```

---

## 🚨 Troubleshooting

### Problem: "Vector Store is not initialized"

**Решение:**
1. Чекај 30 секунди за иницијализација
2. Провери логови за грешки
3. Рестартувај API

### Problem: "OpenAI API Error"

**Решение:**
1. Провери дали `OPENAI_API_KEY` е set
2. Провери дали имаш credits на OpenAI
3. Провери internet connectivity

### Problem: "No results found"

**Решение:**
1. Намали `minSimilarity` threshold (0.3-0.5)
2. Користи поопшти термини
3. Провери дали embeddings се генерирани: `curl .../stats`

---

## 📚 Документација

- **Целосна документација**: [kb/PHASE3_RAG_COMPLETED.md](PHASE3_RAG_COMPLETED.md)
- **API примери**: [kb/RAG_API_EXAMPLES.md](RAG_API_EXAMPLES.md)
- **Архитектура**: [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md)

---

## 🎉 Success Criteria

Phase 3 е успешна ако:
- ✅ Build е успешен без грешки
- ✅ Health check враќа `Healthy` и `hasDocuments: true`
- ✅ Stats покажува ~9 документи и ~15 chunks
- ✅ Search наоѓа релевантни резултати (similarity > 0.7)
- ✅ RAG прашања враќаат смислени одговори со извори
- ✅ Сите тестови во `test-rag.sh` поминуваат

---

## 🚀 Next Phase

**Phase 4: Real Document Integration**
- PDF parsing (Правилник, Упатства)
- OCR за скенирани документи
- Advanced chunking strategies
- Multi-lingual support (MK/EN)
- User feedback loop

---

**Happy Testing! 🎊**
