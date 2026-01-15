# Локална Инсталација - LON System

## Prerequisite

### Windows
1. **.NET 8 SDK**: https://dotnet.microsoft.com/download/dotnet/8.0
2. **SQL Server** (LocalDB, Express, или Full):
   - LocalDB: https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb
   - Express: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
3. **Node.js** (за frontend): https://nodejs.org/

### Mac/Linux
1. **.NET 8 SDK**: https://dotnet.microsoft.com/download/dotnet/8.0
2. **SQL Server** (Docker или Azure SQL):
   ```bash
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
      -p 1433:1433 --name sql-server \
      -d mcr.microsoft.com/mssql/server:2022-latest
   ```
3. **Node.js**: https://nodejs.org/

## Брза Инсталација

### Метод 1: Автоматска Скрипта (Препорачано)

#### Windows (PowerShell)
```powershell
# Оди во проектот
cd C:\path\to\LON-test

# Изврши миграции
.\scripts\run-migrations.ps1

# Стартувај API
dotnet run --project src/LON.API/LON.API.csproj
```

#### Mac/Linux (Bash)
```bash
# Оди во проектот
cd /path/to/LON-test

# Направи скриптата извршна
chmod +x scripts/run-migrations.sh

# Изврши миграции
./scripts/run-migrations.sh

# Стартувај API
dotnet run --project src/LON.API/LON.API.csproj
```

### Метод 2: Рачна Инсталација

#### 1. Инсталирај EF Tools
```bash
dotnet tool install --global dotnet-ef
```

#### 2. Конфигурирај Connection String

Измени `src/LON.API/appsettings.Development.json`:

**Windows (LocalDB/Express):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=LONDB;Integrated Security=True;TrustServerCertificate=True;"
  }
}
```

**Mac/Linux/Docker SQL:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=LONDB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
  }
}
```

#### 3. Изврши Миграции
```bash
cd src/LON.API
dotnet ef database update --project ../LON.Infrastructure/LON.Infrastructure.csproj
```

#### 4. Стартувај API
```bash
dotnet run
```

API ќе биде достапен на: http://localhost:5000

## Верификација

### Провери дали работи:
```bash
# Health check
curl http://localhost:5000/health

# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}'
```

### Default Login:
- **Username**: `admin`
- **Password**: `Admin123!`

## Frontend Setup (Опционално)

```bash
cd frontend/web
npm install
npm start
```

Frontend ќе биде достапен на: http://localhost:3000

## Troubleshooting

### Problem: "Invalid object name 'Users'"
**Причина**: Миграциите не се извршени

**Решение**:
```bash
cd src/LON.API
dotnet ef database update --project ../LON.Infrastructure/LON.Infrastructure.csproj
```

### Problem: "Cannot connect to SQL Server"
**Причина**: SQL Server не работи или connection string е погрешен

**Решение (Windows)**:
1. Провери дали SQL Server работи:
   - Отвори "Services" (Win+R → services.msc)
   - Побарај "SQL Server" и стартувај го

**Решение (Mac/Linux)**:
```bash
docker ps | grep sql-server
# Ако не работи:
docker start sql-server
```

### Problem: "dotnet-ef command not found"
**Причина**: Entity Framework Tools не се инсталирани

**Решение**:
```bash
dotnet tool install --global dotnet-ef
# Или ажурирај:
dotnet tool update --global dotnet-ef
```

### Problem: Port 5000 already in use
**Решение**: Промени порта во `src/LON.API/Properties/launchSettings.json`

## Development Tips

### Hot Reload
```bash
dotnet watch run --project src/LON.API/LON.API.csproj
```

### Database Reset (Внимание: ги брише сите податоци!)
```bash
cd src/LON.API
dotnet ef database drop --force --project ../LON.Infrastructure/LON.Infrastructure.csproj
dotnet ef database update --project ../LON.Infrastructure/LON.Infrastructure.csproj
```

### Креирај нова миграција
```bash
cd src/LON.API
dotnet ef migrations add MigrationName --project ../LON.Infrastructure/LON.Infrastructure.csproj
```

### Провери дали има pending миграции
```bash
cd src/LON.API
dotnet ef migrations list --project ../LON.Infrastructure/LON.Infrastructure.csproj
```

## Visual Studio Setup

1. Отвори `LON.sln`
2. Постави `LON.API` како Startup Project
3. Провери Connection String во `appsettings.Development.json`
4. Package Manager Console:
   ```
   Update-Database -Project LON.Infrastructure -StartupProject LON.API
   ```
5. Press F5 за да стартуваш

## VS Code Setup

1. Отвори фолдерот во VS Code
2. Инсталирај C# extension
3. Press F5 или:
   ```bash
   dotnet run --project src/LON.API/LON.API.csproj
   ```

## Production Deployment

За production, користи Docker compose:
```bash
docker-compose up -d
```

Повеќе информации: [DEPLOYMENT.md](docs/DEPLOYMENT.md)

## Следни Чекори

1. ✅ Извршени миграции
2. ✅ API работи на http://localhost:5000
3. 📝 Тестирај endpoints преку Swagger: http://localhost:5000/swagger
4. 🎨 Стартувај Frontend: `cd frontend/web && npm start`
5. 📚 Прочитај документација: [docs/README.md](docs/README.md)

## Помош

Ако имаш проблеми:
1. Провери дали сите prerequisite се инсталирани
2. Провери connection string
3. Провери дали SQL Server работи
4. Погледни логови во конзолата
5. Провери firewall/antivirus settings
