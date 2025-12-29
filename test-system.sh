#!/bin/bash

echo "🔍 LON System - Health Check & Data Validation"
echo "=============================================="
echo ""

# Провери дали SQL Server е готов
echo "1️⃣ Checking SQL Server..."
until docker exec lon-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -C -Q "SELECT 1" > /dev/null 2>&1; do
    echo "   ⏳ Waiting for SQL Server to start..."
    sleep 5
done
echo "   ✅ SQL Server is ready!"
echo ""

# Провери дали API е готов
echo "2️⃣ Checking API..."
until curl -s http://localhost:5000/health > /dev/null 2>&1; do
    echo "   ⏳ Waiting for API to start..."
    sleep 5
done
echo "   ✅ API is ready!"
echo ""

# Провери TARIC податоци
echo "3️⃣ Checking TARIC data in database..."
TARIC_COUNT=$(docker exec lon-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -C -d LONDB -Q "SELECT COUNT(*) as Count FROM TariffCodes" -h -1 -W 2>&1 | grep -o '[0-9]*' | head -1)

if [ ! -z "$TARIC_COUNT" ] && [ "$TARIC_COUNT" -gt 0 ]; then
    echo "   ✅ TARIC codes loaded: $TARIC_COUNT records"
else
    echo "   ⚠️  TARIC codes not found or still loading..."
fi
echo ""

# Провери Regulations
echo "4️⃣ Checking Customs Regulations..."
REG_COUNT=$(docker exec lon-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -C -d LONDB -Q "SELECT COUNT(*) as Count FROM CustomsRegulations" -h -1 -W 2>&1 | grep -o '[0-9]*' | head -1)

if [ ! -z "$REG_COUNT" ] && [ "$REG_COUNT" -gt 0 ]; then
    echo "   ✅ Customs Regulations loaded: $REG_COUNT records"
else
    echo "   ⚠️  Customs Regulations not found or still loading..."
fi
echo ""

# Провери Code Lists
echo "5️⃣ Checking Code Lists..."
CODELIST_COUNT=$(docker exec lon-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -C -d LONDB -Q "SELECT COUNT(*) as Count FROM CodeListItems" -h -1 -W 2>&1 | grep -o '[0-9]*' | head -1)

if [ ! -z "$CODELIST_COUNT" ] && [ "$CODELIST_COUNT" -gt 0 ]; then
    echo "   ✅ Code List Items loaded: $CODELIST_COUNT records"
else
    echo "   ⚠️  Code List Items not found or still loading..."
fi
echo ""

# Провери Declaration Rules
echo "6️⃣ Checking Declaration Rules..."
RULES_COUNT=$(docker exec lon-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -C -d LONDB -Q "SELECT COUNT(*) as Count FROM DeclarationRules" -h -1 -W 2>&1 | grep -o '[0-9]*' | head -1)

if [ ! -z "$RULES_COUNT" ] && [ "$RULES_COUNT" -gt 0 ]; then
    echo "   ✅ Declaration Rules loaded: $RULES_COUNT records"
else
    echo "   ⚠️  Declaration Rules not found or still loading..."
fi
echo ""

# Прикажи примери од TARIC
echo "7️⃣ Sample TARIC records:"
docker exec lon-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -C -d LONDB -Q "SELECT TOP 3 TariffNumber, LEFT(Description, 50) + '...' as Description, CustomsRate, VATRate FROM TariffCodes WHERE IsActive = 1 ORDER BY TariffNumber" -s "," -W
echo ""

echo "=============================================="
echo "✅ Health check completed!"
echo ""
echo "📊 Summary:"
echo "   - TARIC Codes: $TARIC_COUNT"
echo "   - Regulations: $REG_COUNT"
echo "   - Code Lists: $CODELIST_COUNT"
echo "   - Rules: $RULES_COUNT"
echo ""
echo "🌐 Access Points:"
echo "   - API: http://localhost:5000"
echo "   - Swagger: http://localhost:5000/swagger"
echo "   - Frontend: http://localhost:3000"
echo ""
