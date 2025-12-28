# Deployment Guide

## Prerequisites

- **Docker** 20.10+ & **Docker Compose** 1.29+
- **Git** (за клонирање на репозиторијата)
- **Ports:** 1433 (SQL Server), 5000 (API), 3000 (Web Frontend)

### For Local Development (without Docker):
- **.NET 8 SDK**
- **Node.js 18+** & **npm**
- **SQL Server 2022** (или Docker container)
- **Flutter SDK 3.0+** (за mobile)

---

## Quick Start with Docker Compose

### 1. Clone Repository

```bash
git clone <repository-url>
cd LON-test
```

### 2. Build and Run

```bash
docker-compose up --build
```

Оваа команда ќе:
- Стартува SQL Server на `localhost:1433`
- Билда и стартува API на `http://localhost:5000`
- Билда и стартува Worker (background service)
- Билда и стартува React frontend на `http://localhost:3000`

### 3. Access Application

- **Web UI:** http://localhost:3000
- **API:** http://localhost:5000
- **Swagger:** http://localhost:5000/swagger
- **SQL Server:** localhost:1433
  - User: `sa`
  - Password: `YourStrong@Passw0rd`

### 4. Default Credentials

```
Username: admin@lon.local
Password: Admin@123
```

### 5. Stop Services

```bash
docker-compose down
```

Ако сакаш да ги избришеш и податоците:
```bash
docker-compose down -v
```

---

## Local Development Setup

### Backend (.NET API)

#### 1. Restore Dependencies

```bash
cd src/LON.API
dotnet restore
```

#### 2. Update Connection String

Едитирај `src/LON.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=LONDB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
  }
}
```

#### 3. Create Database

```bash
# Ако имаш SQL Server локално
sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -Q "CREATE DATABASE LONDB"

# Или со Docker
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
  -p 1433:1433 --name sqlserver -d mcr.microsoft.com/mssql/server:2022-latest
```

#### 4. Apply Migrations

```bash
cd src/LON.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../LON.API/LON.API.csproj
dotnet ef database update --startup-project ../LON.API/LON.API.csproj
```

#### 5. Run API

```bash
cd ../LON.API
dotnet run
```

API ќе биде достапен на `https://localhost:5001` или `http://localhost:5000`

#### 6. Run Worker

```bash
cd ../LON.Worker
dotnet run
```

### Frontend (React)

#### 1. Install Dependencies

```bash
cd frontend/web
npm install
```

#### 2. Configure API URL

Едитирај `frontend/web/src/api.ts`:

```typescript
const API_BASE_URL = 'http://localhost:5000/api';
```

#### 3. Run Development Server

```bash
npm start
```

Frontend ќе биде достапен на `http://localhost:3000`

#### 4. Build for Production

```bash
npm run build
```

### Mobile (Flutter)

#### 1. Install Dependencies

```bash
cd frontend/mobile
flutter pub get
```

#### 2. Configure API URL

Едитирај `frontend/mobile/lib/main.dart`:

```dart
const String API_BASE_URL = 'http://10.0.2.2:5000/api'; // For Android Emulator
// const String API_BASE_URL = 'http://localhost:5000/api'; // For iOS Simulator
```

#### 3. Run on Emulator/Device

```bash
flutter run
```

#### 4. Build APK

```bash
flutter build apk --release
```

---

## Database Migrations

### Create New Migration

```bash
cd src/LON.Infrastructure
dotnet ef migrations add <MigrationName> --startup-project ../LON.API/LON.API.csproj
```

### Apply Migrations

```bash
dotnet ef database update --startup-project ../LON.API/LON.API.csproj
```

### Remove Last Migration

```bash
dotnet ef migrations remove --startup-project ../LON.API/LON.API.csproj
```

### Generate SQL Script

```bash
dotnet ef migrations script --startup-project ../LON.API/LON.API.csproj -o migration.sql
```

---

## Environment Variables

### API (LON.API)

```bash
ConnectionStrings__DefaultConnection=Server=sqlserver;Database=LONDB;...
JwtSettings__Secret=YourSuperSecretKeyForJWTTokenGeneration123!
JwtSettings__Issuer=LON.API
JwtSettings__Audience=LON.Web
JwtSettings__ExpiryMinutes=60
```

### Worker (LON.Worker)

```bash
ConnectionStrings__DefaultConnection=Server=sqlserver;Database=LONDB;...
Worker__ProcessingIntervalSeconds=10
```

### Frontend (React)

```bash
REACT_APP_API_URL=http://localhost:5000/api
```

---

## Production Deployment

### Docker Compose (Recommended)

#### 1. Update Environment Variables

Креирај `.env` file:

```env
# SQL Server
SA_PASSWORD=YourStrongPassword!@#
MSSQL_PID=Express

# API
JWT_SECRET=YourProductionJWTSecretKey123!@#
JWT_ISSUER=LON.API
JWT_AUDIENCE=LON.Web

# Connection String
DB_CONNECTION=Server=sqlserver;Database=LONDB;User Id=sa;Password=YourStrongPassword!@#;TrustServerCertificate=True;
```

#### 2. Update docker-compose.yml

```yaml
services:
  sqlserver:
    environment:
      - SA_PASSWORD=${SA_PASSWORD}
  
  api:
    environment:
      - ConnectionStrings__DefaultConnection=${DB_CONNECTION}
      - JwtSettings__Secret=${JWT_SECRET}
```

#### 3. Deploy

```bash
docker-compose -f docker-compose.prod.yml up -d
```

### Kubernetes (Advanced)

```bash
# Create namespace
kubectl create namespace lon-production

# Apply secrets
kubectl create secret generic lon-db-secret \
  --from-literal=sa-password='YourStrongPassword' \
  -n lon-production

# Apply deployments
kubectl apply -f k8s/sqlserver-deployment.yaml
kubectl apply -f k8s/api-deployment.yaml
kubectl apply -f k8s/worker-deployment.yaml
kubectl apply -f k8s/frontend-deployment.yaml

# Apply services
kubectl apply -f k8s/services.yaml
```

### Azure App Service

#### Backend (API)

```bash
# Create App Service
az webapp create --name lon-api \
  --resource-group lon-rg \
  --plan lon-plan \
  --runtime "DOTNETCORE:8.0"

# Configure Connection String
az webapp config connection-string set \
  --name lon-api \
  --resource-group lon-rg \
  --settings DefaultConnection="Server=lon-sql.database.windows.net;..." \
  --connection-string-type SQLAzure

# Deploy
cd src/LON.API
dotnet publish -c Release
az webapp deployment source config-zip \
  --src publish.zip \
  --name lon-api \
  --resource-group lon-rg
```

#### Frontend (Static Web App)

```bash
# Create Static Web App
az staticwebapp create \
  --name lon-frontend \
  --resource-group lon-rg \
  --source https://github.com/<your-repo> \
  --location "West Europe" \
  --branch main \
  --app-location "/frontend/web" \
  --output-location "build"
```

---

## Monitoring & Logging

### Application Insights (Azure)

```bash
# Install package
dotnet add package Microsoft.ApplicationInsights.AspNetCore

# Configure in appsettings.json
{
  "ApplicationInsights": {
    "InstrumentationKey": "your-key"
  }
}

# Update Program.cs
builder.Services.AddApplicationInsightsTelemetry();
```

### Serilog (Structured Logging)

```bash
# Install packages
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File

# Configure in Program.cs
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));
```

---

## Backup & Restore

### SQL Server Backup

```bash
# Backup
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P YourStrong@Passw0rd \
  -Q "BACKUP DATABASE LONDB TO DISK='/var/opt/mssql/data/LONDB.bak'"

# Copy backup from container
docker cp sqlserver:/var/opt/mssql/data/LONDB.bak ./backups/

# Restore
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P YourStrong@Passw0rd \
  -Q "RESTORE DATABASE LONDB FROM DISK='/var/opt/mssql/data/LONDB.bak' WITH REPLACE"
```

---

## Troubleshooting

### API не стартува

**Problem:** Connection refused или timeout

**Solution:**
- Провери дали SQL Server е достапен: `sqlcmd -S localhost -U sa -P YourStrong@Passw0rd`
- Провери connection string во `appsettings.json`
- Проверете firewall правила

### Миграции фејлуваат

**Problem:** Cannot apply migrations

**Solution:**
```bash
# Провери дали database постои
sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -Q "SELECT name FROM sys.databases"

# Креирај database manually
sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -Q "CREATE DATABASE LONDB"

# Retry migration
dotnet ef database update --startup-project ../LON.API/LON.API.csproj
```

### Frontend не се поврзува со API

**Problem:** CORS errors

**Solution:**
- Провери дали API е стартуван
- Провери CORS конфигурација во `Program.cs`:
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});
```

### Docker Compose не стартува

**Problem:** Port already in use

**Solution:**
```bash
# Најди процеси на порт 1433
lsof -i :1433
# или
netstat -ano | findstr :1433

# Убиј процес
kill -9 <PID>

# Или промени порт во docker-compose.yml
ports:
  - "1434:1433"
```

---

## Performance Optimization

### Database Indexes

```sql
-- Inventory queries
CREATE NONCLUSTERED INDEX IX_InventoryBalance_Item_Location
ON InventoryBalances (ItemId, LocationId, QualityStatus)
INCLUDE (Quantity);

-- Traceability queries
CREATE NONCLUSTERED INDEX IX_TraceLink_Source
ON TraceLinks (SourceBatchNumber, SourceMRN);

CREATE NONCLUSTERED INDEX IX_TraceLink_Target
ON TraceLinks (TargetBatchNumber, TargetMRN);

-- Guarantee queries
CREATE NONCLUSTERED INDEX IX_GuaranteeLedger_MRN
ON GuaranteeLedgerEntries (MRN, IsReleased);
```

### API Response Caching

```csharp
// In Program.cs
builder.Services.AddResponseCaching();
app.UseResponseCaching();

// In Controller
[ResponseCache(Duration = 60)]
[HttpGet("dashboard")]
public async Task<IActionResult> GetDashboard()
{
    // ...
}
```

---

## Security Checklist

- [ ] Промени default JWT secret
- [ ] Промени SQL Server SA password
- [ ] Enable HTTPS во production
- [ ] Configure proper CORS policies
- [ ] Implement rate limiting
- [ ] Enable SQL Server firewall rules
- [ ] Користи Azure Key Vault за secrets
- [ ] Рregular security updates
- [ ] Implement proper error handling (не експонирај stack trace)
- [ ] Enable audit logging

---

## Support & Maintenance

### Logs Location

- **API Logs:** `src/LON.API/logs/`
- **Worker Logs:** `src/LON.Worker/logs/`
- **Docker Logs:** `docker logs <container-name>`

### Health Checks

```bash
# API Health
curl http://localhost:5000/health

# Database Connection
curl http://localhost:5000/health/db
```

### Regular Maintenance

- **Daily:** Monitor error logs
- **Weekly:** Database backup
- **Monthly:** Review performance metrics, update dependencies
- **Quarterly:** Security audit, load testing
