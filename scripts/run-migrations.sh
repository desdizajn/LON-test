#!/bin/bash
# Bash скрипта за извршување на миграции (Linux/Mac)

echo "🔧 LON System - Database Migration Script"
echo "========================================="
echo ""

# Провери дали постои dotnet
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK не е инсталиран!"
    echo "Преземи од: https://dotnet.microsoft.com/download"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "✅ .NET SDK Version: $DOTNET_VERSION"
echo ""

# Најди го патот до проектот
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$( cd "$SCRIPT_DIR/.." && pwd )"
API_PATH="$ROOT_DIR/src/LON.API"
INFRA_PATH="$ROOT_DIR/src/LON.Infrastructure"

echo "📂 Project Path: $ROOT_DIR"
echo ""

# Провери дали постојат проектите
if [ ! -d "$API_PATH" ]; then
    echo "❌ LON.API проект не е пронајден на: $API_PATH"
    exit 1
fi

if [ ! -d "$INFRA_PATH" ]; then
    echo "❌ LON.Infrastructure проект не е пронајден на: $INFRA_PATH"
    exit 1
fi

echo "✅ Проекти пронајдени"
echo ""

# Провери дали е инсталиран dotnet ef
echo "Проверка на Entity Framework Tools..."
if ! dotnet tool list -g | grep -q "dotnet-ef"; then
    echo "⚠️  Entity Framework Tools не се инсталирани"
    echo "Инсталирам dotnet-ef..."
    dotnet tool install --global dotnet-ef
    if [ $? -ne 0 ]; then
        echo "❌ Грешка при инсталација на dotnet-ef"
        exit 1
    fi
    echo "✅ dotnet-ef инсталиран"
else
    echo "✅ Entity Framework Tools инсталирани"
fi

echo ""
echo "========================================="
echo "🚀 Извршување на миграции..."
echo "========================================="
echo ""

# Оди во API директориумот
cd "$API_PATH"

# Изврши миграции
echo "Команда: dotnet ef database update --project ../LON.Infrastructure/LON.Infrastructure.csproj"
echo ""

dotnet ef database update --project ../LON.Infrastructure/LON.Infrastructure.csproj --startup-project LON.API.csproj

if [ $? -eq 0 ]; then
    echo ""
    echo "========================================="
    echo "✅ Миграциите се успешно извршени!"
    echo "========================================="
    echo ""
    echo "Сега можеш да го стартуваш API-то со:"
    echo "  dotnet run --project src/LON.API/LON.API.csproj"
    echo ""
    echo "Login креденцијали:"
    echo "  Username: admin"
    echo "  Password: Admin123!"
    echo ""
else
    echo ""
    echo "========================================="
    echo "❌ Грешка при извршување на миграции!"
    echo "========================================="
    echo ""
    echo "Можни причини:"
    echo "1. SQL Server не работи"
    echo "2. Немаш пермисии за креирање база"
    echo "3. Connection string е погрешен"
    echo ""
    echo "Провери:"
    echo "- Connection string во src/LON.API/appsettings.Development.json"
    echo ""
    exit 1
fi

# Врати се назад
cd "$ROOT_DIR"
