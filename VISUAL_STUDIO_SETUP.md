# LON System - Visual Studio Development Setup

## Quick Start Guide

Овој документ опишува како да ја подготвиш LON апликацијата за локален development во Visual Studio со SQL Server, **без Docker**.

---

## Prerequisiti

- ✅ Visual Studio 2022 (17.8 или понова верзија)
- ✅ SQL Server 2019+ (Express, Developer, или Standard)
- ✅ .NET 8.0 SDK
- ✅ Node.js 18+ и npm
- ✅ Git (за клонирање на кодот)

---

## Чекор 1: Конфигурација на SQL Server

### Опција A: Windows Authentication (Препорачано за локален development)

Веќе е конфигурирано во `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=LONDB;Integrated Security=True;TrustServerCertificate=True;"
  }
}
```

### Опција B: SQL Server Authentication

Ако користиш SQL Authentication, промени connection string:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=LONDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True;"
  }
}
```

### Опција C: Named Instance (пр. SQLEXPRESS)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=LONDB;Integrated Security=True;TrustServerCertificate=True;"
  }
}
```

### Провери дали SQL Server работи:

**Windows:**
1. SQL Server Configuration Manager
2. Провери дали "SQL Server (MSSQLSERVER)" service е Running
3. Провери дали TCP/IP протоколот е Enabled

**Тест со sqlcmd:**
```bash
sqlcmd -S localhost -E
# Ако работи, ќе видиш: 1>
```

---

## Чекор 2: Отвори проектот во Visual Studio

1. **Clone repository** (ако не е веќе клониран):
   ```bash
   git clone <repository-url>
   cd LON-test
   ```

2. **Отвори Solution:**
   - Double-click на `LON.sln`
   - Или од Visual Studio: File → Open → Project/Solution → избери `LON.sln`

3. **Set Startup Project:**
   - Right-click на `LON.API` project → Set as Startup Project

---

## Чекор 3: Примени Database Migrations

Ова ќе креира LONDB база и ќе ги наполни табелите со seed data.

### Метод A: Package Manager Console (во Visual Studio)

1. Отвори: **Tools → NuGet Package Manager → Package Manager Console**
2. Провери дали е селектиран `LON.Infrastructure` како Default project
3. Run:
   ```powershell
   Update-Database
   ```

### Метод B: Terminal (dotnet CLI)

```bash
cd src/LON.API
dotnet ef database update
```

### Што прави миграцијата?

- ✅ Креира `LONDB` база на SQL Server
- ✅ Креира табели: Users, Employees, Roles, Permissions, Shifts, WorkCenters, Machines, итн.
- ✅ Автоматски seed-ува master data (праздници, країни, валути)
- ✅ Креира admin корисник:
  - Username: `admin`
  - Password: `Admin123!`
- ✅ Креира Role-ови: Administrator, WarehouseManager, ProductionManager, CustomsOfficer

---

## Чекор 4: Run API од Visual Studio

1. **Стартувај debugging:**
   - Press **F5** (или Debug → Start Debugging)
   - Или **Ctrl+F5** за run without debugging

2. **Провери дали API работи:**
   - Browser ќе се отвори автоматски на: `http://localhost:5000/swagger`
   - Или manually провери: `http://localhost:5000/api/health`

3. **Провери Console логови:**
   - Треба да видиш:
     ```
     info: LON.API[0]
           ✅ Database migration completed
     info: LON.API[0]
           ✅ Seed data completed
     info: LON.API[0]
           ✅ User management seed completed
     ```

### Забелешка: Brз Startup

VectorStoreInitializer е **оневозможен** за development (беше причина за 10-15 минути blocking):
- ✅ Коментиран во `Program.cs` (lines 111-113)
- ✅ `"EnableVectorStore": false` во appsettings.Development.json

API сега стартува за **3-5 секунди** наместо 10-15 минути! 🚀

---

## Чекор 5: Run Frontend (React)

### A. Install Dependencies (само прв пат)

```bash
cd frontend/web
npm install --legacy-peer-deps
```

**Зошто `--legacy-peer-deps`?**
- TypeScript 4.9.5 vs react-scripts 5.0.1 peer dependency conflict
- Решено со downgrade на TypeScript верзијата

### B. Провери API Proxy

Едитирај `frontend/web/package.json` - провери дали постои:
```json
{
  "proxy": "http://localhost:5000"
}
```

### C. Run Development Server

```bash
npm start
```

- Frontend ќе се отвори на: `http://localhost:3000`
- Hot reload е enabled - промените се apply-уваат автоматски

---

## Чекор 6: Test Login

1. Navigate to: `http://localhost:3000/login`
2. Enter credentials:
   - **Username:** `admin`
   - **Password:** `Admin123!`
3. Кликни "Sign In"
4. Треба да бидеш redirect-иран на Dashboard
5. Треба да ги видиш admin модулите:
   - 📦 Warehouse Management
   - 🏭 Production
   - 🛃 Customs
   - 🔐 User Management
   - 👥 Employee Management
   - 📅 Shift Management
   - 🔑 Roles & Permissions

---

## Верификација на Setup

### 1. Провери SQL Server база

Отвори **SQL Server Management Studio** (SSMS) или **Azure Data Studio**:

```sql
USE LONDB;

-- Провери admin корисник
SELECT Id, Username, Email, IsActive FROM Users WHERE Username = 'admin';

-- Провери role-ови
SELECT Id, Name, Description FROM Roles;

-- Провери permissions
SELECT Id, Name, Resource, Description FROM Permissions;

-- Провери UserRoles mapping
SELECT u.Username, r.Name AS RoleName
FROM Users u
JOIN UserRoles ur ON u.Id = ur.UserId
JOIN Roles r ON ur.RoleId = r.RoleId
WHERE u.Username = 'admin';
```

Треба да видиш:
- 1 admin user
- 4 roles (Administrator, WarehouseManager, ProductionManager, CustomsOfficer)
- 36 permissions grouped by resource (User, Role, Employee, Shift, Machine, WMS, Production, Customs, LON)

### 2. Test API со Swagger

1. Go to: `http://localhost:5000/swagger`
2. Expand **POST /api/auth/login**
3. Click "Try it out"
4. Enter:
   ```json
   {
     "username": "admin",
     "password": "Admin123!"
   }
   ```
5. Execute
6. Треба да добиеш Response 200 со:
   ```json
   {
     "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
     "refreshToken": "...",
     "expiresAt": "2025-01-20T15:30:00Z",
     "user": {
       "id": 1,
       "username": "admin",
       "email": "admin@lon.local",
       ...
     }
   }
   ```

### 3. Test Frontend Login

1. Open Browser Console (F12)
2. Navigate to: `http://localhost:3000/login`
3. Login со admin/Admin123!
4. Провери Network tab - треба да видиш:
   - POST `http://localhost:5000/api/auth/login` → 200 OK
   - Response содржи accessToken
5. Провери Application tab → Local Storage:
   - `token` - JWT token
   - `user` - User object со fullName, roles, permissions

---

## Troubleshooting

### ❌ Problem: "Cannot open database 'LONDB'"

**Solution:**
1. Run migration:
   ```powershell
   Update-Database
   ```
2. Refresh SQL Server Object Explorer во Visual Studio
3. Verify database exists in SSMS

---

### ❌ Problem: "Login failed for user 'NT AUTHORITY\\ANONYMOUS LOGON'"

**Solution:**
1. Windows Authentication не работи - користи SQL Authentication
2. Промени connection string:
   ```json
   "Server=localhost;Database=LONDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True;"
   ```
3. Или додади current Windows user на SQL Server:
   ```sql
   USE master;
   CREATE LOGIN [DOMAIN\YourUsername] FROM WINDOWS;
   ALTER SERVER ROLE sysadmin ADD MEMBER [DOMAIN\YourUsername];
   ```

---

### ❌ Problem: "A network-related error occurred"

**Solutions:**
1. **Провери дали SQL Server работи:**
   - SQL Server Configuration Manager → SQL Server Services → Провери Status
   - Or: `Get-Service MSSQLSERVER` (PowerShell)

2. **Enable TCP/IP:**
   - SQL Server Configuration Manager
   - SQL Server Network Configuration → Protocols for MSSQLSERVER
   - Enable TCP/IP
   - Restart SQL Server service

3. **Check Firewall:**
   ```powershell
   New-NetFirewallRule -DisplayName "SQL Server" -Direction Inbound -Protocol TCP -LocalPort 1433 -Action Allow
   ```

4. **Test Connection:**
   ```bash
   sqlcmd -S localhost -E
   # Or with SQL Auth:
   sqlcmd -S localhost -U sa -P YourPassword
   ```

---

### ❌ Problem: API startup takes 10-15 minutes

**Solution:**
✅ **Веќе решено!** VectorStoreInitializer е оневозможен.

Ако се случи:
1. Провери `Program.cs` lines 111-113 - треба да бидат закоментирани:
   ```csharp
   // using var scope = app.Services.CreateScope();
   // var vectorStoreInit = scope.ServiceProvider.GetRequiredService<VectorStoreInitializer>();
   // await vectorStoreInit.InitializeAsync();
   ```

2. Провери `appsettings.Development.json`:
   ```json
   "EnableVectorStore": false
   ```

---

### ❌ Problem: Frontend `npm install` fails with ERESOLVE

**Solution:**
```bash
# Delete existing files
rm -rf node_modules package-lock.json

# Install with legacy peer deps flag
npm install --legacy-peer-deps
```

TypeScript е downgrade-иран на 4.9.5 во `package.json` за compatibility со react-scripts 5.0.1.

---

### ❌ Problem: Login fails with "Failed to fetch"

**Checks:**
1. **API не работи:**
   - Proveri `http://localhost:5000/api/health` во browser
   - Стартувај API од Visual Studio (F5)

2. **CORS error:**
   - Proveri browser console за "CORS policy" error
   - Verify `Program.cs` има "AllowAll" policy (development only)

3. **Wrong API URL:**
   - Proveri `frontend/web/package.json` има `"proxy": "http://localhost:5000"`

4. **Port conflict:**
   - Провери дали нешто друго не користи port 5000
   - Промени port во `Properties/launchSettings.json`:
     ```json
     "applicationUrl": "http://localhost:5001"
     ```
   - Update proxy во frontend: `"proxy": "http://localhost:5001"`

---

### ❌ Problem: "The admin password is incorrect"

**Root Cause:**
- Password се хешира со BCrypt
- Seed data креира hash од "Admin123!"

**Solution:**
1. **Reset admin password:**
   ```sql
   USE LONDB;
   UPDATE Users 
   SET PasswordHash = '$2a$11$YourNewHashHere' 
   WHERE Username = 'admin';
   ```

2. **Or delete and re-seed:**
   ```sql
   DELETE FROM Users WHERE Username = 'admin';
   ```
   Restart API - seed ќе го креира повторно.

3. **Verify password во backend:**
   - Seed code во `UserManagementSeed.cs`:
     ```csharp
     PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!")
     ```

---

## Следни Чекори

### Phase A Testing

Сега можеш да започнеш со тестирање на **User Management** модулот:

1. **User Management UI:**
   - Create new user
   - Assign roles
   - Edit user details
   - Deactivate user

2. **Employee Management UI:**
   - Create employee (автоматски креира User)
   - Assign shift
   - Edit employee details

3. **Shift Management UI:**
   - Create shifts (Morning, Afternoon, Night)
   - Edit shift times

4. **Role Management UI:**
   - Create custom role
   - Assign permissions grouped by resource

### Следни Features (Gap Analysis)

Од `docs/PRE_TESTING_ANALYSIS.md`:

1. **Machine/WorkCenter Management UI** (~2 часа)
   - Backend веќе постои
   - Треба да се креира React UI

2. **Multi-language Support (i18n)** (~4 часа)
   - react-i18next integration
   - Translation files за MK/EN

3. **Employee-Machine Assignment** (~6 часа)
   - Нова табела: EmployeeMachineAssignment
   - Track кој вработен на која машина работи
   - Time tracking (Clock In/Out)
   - Production output tracking

---

## Performance Tips

### 1. Database Indexing
Migrations веќе вклучуваат indexes на foreign keys, но можеш да додадеш custom:

```sql
CREATE INDEX IX_Employees_ShiftId ON Employees(ShiftId);
CREATE INDEX IX_Users_Username ON Users(Username);
CREATE INDEX IX_Users_Email ON Users(Email);
```

### 2. API Caching
За production, додај Redis caching:
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});
```

### 3. Frontend Build Optimization
```bash
npm run build
```
Optimized build ќе биде во `frontend/web/build/` folder.

---

## Production Deployment

Кога ќе завршиш development и тестирање:

### Docker Compose (Recommended)

```bash
# Build and deploy all services
docker-compose up --build -d
```

Services:
- API: `http://localhost:5000`
- Frontend: `http://localhost:80`
- SQL Server: `localhost:1433`
- Worker: Background service

### Azure App Service

1. Publish API:
   - Right-click `LON.API` → Publish
   - Choose Azure App Service
   - Configure connection string во Configuration

2. Deploy Frontend:
   ```bash
   cd frontend/web
   npm run build
   az webapp up --name lon-frontend --resource-group LON-RG --html
   ```

---

## Summary Checklist

- [x] SQL Server installed and running
- [x] Visual Studio 2022 opened with LON.sln
- [x] appsettings.Development.json configured
- [x] Database migration applied (Update-Database)
- [x] API running on http://localhost:5000
- [x] Swagger accessible at http://localhost:5000/swagger
- [x] Frontend dependencies installed (npm install --legacy-peer-deps)
- [x] Frontend running on http://localhost:3000
- [x] Login successful with admin/Admin123!
- [x] Dashboard shows admin modules

---

## Поддршка

Ако имаш проблеми:

1. **Check logs:**
   - Visual Studio Output window
   - Browser Console (F12)
   - SQL Server Error Log

2. **Verify configuration:**
   - Connection string во appsettings.Development.json
   - Proxy setting во package.json
   - Port availability (5000, 3000)

3. **Database state:**
   ```sql
   SELECT * FROM Users WHERE Username = 'admin';
   ```

4. **API health:**
   ```bash
   curl http://localhost:5000/api/health
   ```

---

**Среќно кодирање! 🚀**
