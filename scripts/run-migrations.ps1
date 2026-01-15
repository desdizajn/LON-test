# PowerShell скрипта за извршување на миграции (Windows локално)

Write-Host "🔧 LON System - Database Migration Script" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Провери дали постои dotnet
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET SDK Version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET SDK не е инсталиран!" -ForegroundColor Red
    Write-Host "Преземи од: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Провери дали постои SQL Server
Write-Host ""
Write-Host "Проверка на SQL Server конекција..." -ForegroundColor Yellow

$connectionString = "Server=localhost;Database=LONDB;Integrated Security=True;TrustServerCertificate=True;"

# Најди го патот до проектот
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = Split-Path -Parent $scriptPath
$apiPath = Join-Path $rootPath "src\LON.API"
$infraPath = Join-Path $rootPath "src\LON.Infrastructure"

Write-Host "📂 Project Path: $rootPath" -ForegroundColor Cyan
Write-Host ""

# Провери дали постојат проектите
if (-not (Test-Path $apiPath)) {
    Write-Host "❌ LON.API проект не е пронајден на: $apiPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $infraPath)) {
    Write-Host "❌ LON.Infrastructure проект не е пронајден на: $infraPath" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Проекти пронајдени" -ForegroundColor Green
Write-Host ""

# Провери дали е инсталиран dotnet ef
Write-Host "Проверка на Entity Framework Tools..." -ForegroundColor Yellow
$efToolsInstalled = dotnet tool list -g | Select-String "dotnet-ef"

if (-not $efToolsInstalled) {
    Write-Host "⚠️  Entity Framework Tools не се инсталирани" -ForegroundColor Yellow
    Write-Host "Инсталирам dotnet-ef..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Грешка при инсталација на dotnet-ef" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ dotnet-ef инсталиран" -ForegroundColor Green
} else {
    Write-Host "✅ Entity Framework Tools инсталирани" -ForegroundColor Green
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "🚀 Извршување на миграции..." -ForegroundColor Yellow
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Оди во API директориумот
Set-Location $apiPath

# Изврши миграции
Write-Host "Команда: dotnet ef database update --project ..\LON.Infrastructure\LON.Infrastructure.csproj" -ForegroundColor Gray
Write-Host ""

dotnet ef database update --project ..\LON.Infrastructure\LON.Infrastructure.csproj --startup-project LON.API.csproj

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "=========================================" -ForegroundColor Green
    Write-Host "✅ Миграциите се успешно извршени!" -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Сега можеш да го стартуваш API-то со:" -ForegroundColor Cyan
    Write-Host "  dotnet run --project src/LON.API/LON.API.csproj" -ForegroundColor White
    Write-Host ""
    Write-Host "Login креденцијали:" -ForegroundColor Cyan
    Write-Host "  Username: admin" -ForegroundColor White
    Write-Host "  Password: Admin123!" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "=========================================" -ForegroundColor Red
    Write-Host "❌ Грешка при извршување на миграции!" -ForegroundColor Red
    Write-Host "=========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Можни причини:" -ForegroundColor Yellow
    Write-Host "1. SQL Server не работи" -ForegroundColor White
    Write-Host "2. Немаш пермисии за креирање база" -ForegroundColor White
    Write-Host "3. Connection string е погрешен" -ForegroundColor White
    Write-Host ""
    Write-Host "Провери:" -ForegroundColor Yellow
    Write-Host "- SQL Server Management Studio (SSMS)" -ForegroundColor White
    Write-Host "- Connection string во src/LON.API/appsettings.Development.json" -ForegroundColor White
    Write-Host ""
    exit 1
}

# Врати се назад
Set-Location $rootPath
