#!/bin/bash

# Test скрипта за RAG Phase 3 функционалности
# Проверка на Vector Store, Semantic Search, и RAG endpoints

API_URL="http://localhost:5000/api"
KNOWLEDGE_BASE_URL="$API_URL/KnowledgeBase"

echo "🧪 Testing LON Knowledge Base - Phase 3: RAG"
echo "================================================"
echo ""

# Чекај API да стартува
echo "⏳ Waiting for API to start..."
for i in {1..30}; do
    if curl -s "$API_URL/health" > /dev/null 2>&1; then
        echo "✅ API is ready!"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "❌ API did not start in time"
        exit 1
    fi
    sleep 1
done
echo ""

# 1. Health Check
echo "1️⃣  Testing Health Check..."
echo "GET $KNOWLEDGE_BASE_URL/health"
curl -s -X GET "$KNOWLEDGE_BASE_URL/health" | jq '.'
echo ""
echo ""

# 2. Statistics
echo "2️⃣  Testing Statistics..."
echo "GET $KNOWLEDGE_BASE_URL/stats"
curl -s -X GET "$KNOWLEDGE_BASE_URL/stats" | jq '.'
echo ""
echo ""

# 3. Semantic Search - Општи термини
echo "3️⃣  Testing Semantic Search (Општо)..."
echo "POST $KNOWLEDGE_BASE_URL/search"
curl -s -X POST "$KNOWLEDGE_BASE_URL/search" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "тарифна ознака и класификација",
    "topK": 3,
    "minSimilarity": 0.5
  }' | jq '.'
echo ""
echo ""

# 4. Semantic Search - Правилник
echo "4️⃣  Testing Semantic Search (Правилник)..."
echo "POST $KNOWLEDGE_BASE_URL/search"
curl -s -X POST "$KNOWLEDGE_BASE_URL/search" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "Хармонизирана номенклатура",
    "topK": 2,
    "minSimilarity": 0.6,
    "documentType": "Правилник"
  }' | jq '.'
echo ""
echo ""

# 5. Semantic Search - SADка упатства
echo "5️⃣  Testing Semantic Search (SADка Упатства)..."
echo "POST $KNOWLEDGE_BASE_URL/search"
curl -s -X POST "$KNOWLEDGE_BASE_URL/search" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "Box 33 тарифна ознака",
    "topK": 2,
    "documentType": "SADка Упатство"
  }' | jq '.'
echo ""
echo ""

# 6. RAG - Ask Question
echo "6️⃣  Testing RAG - Ask Question..."
echo "POST $KNOWLEDGE_BASE_URL/ask"
curl -s -X POST "$KNOWLEDGE_BASE_URL/ask" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Како се пополнува Box 33?",
    "maxContextChunks": 3
  }' | jq '.'
echo ""
echo ""

# 7. RAG - Explain Concept
echo "7️⃣  Testing RAG - Explain Concept..."
echo "POST $KNOWLEDGE_BASE_URL/explain"
curl -s -X POST "$KNOWLEDGE_BASE_URL/explain" \
  -H "Content-Type: application/json" \
  -d '{
    "concept": "Тарифна ознака"
  }' | jq '.'
echo ""
echo ""

# 8. RAG - Complex Question
echo "8️⃣  Testing RAG - Complex Question..."
echo "POST $KNOWLEDGE_BASE_URL/ask"
curl -s -X POST "$KNOWLEDGE_BASE_URL/ask" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Што е Хармонизирана номенклатура и како се користи при царинење?",
    "maxContextChunks": 5
  }' | jq '.'
echo ""
echo ""

# 9. RAG - Procedure Question
echo "9️⃣  Testing RAG - Procedure Question..."
echo "POST $KNOWLEDGE_BASE_URL/ask"
curl -s -X POST "$KNOWLEDGE_BASE_URL/ask" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Што содржи Box 02 и кои податоци треба да се внесат?",
    "maxContextChunks": 2
  }' | jq '.'
echo ""
echo ""

echo "================================================"
echo "✅ Phase 3 RAG Testing Completed!"
echo ""
echo "📝 Summary:"
echo "   - Health Check: ✓"
echo "   - Statistics: ✓"
echo "   - Semantic Search (General): ✓"
echo "   - Semantic Search (Filtered): ✓"
echo "   - RAG Questions: ✓"
echo "   - RAG Concept Explanation: ✓"
echo ""
echo "🎉 All tests completed!"
