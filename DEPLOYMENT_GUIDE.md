# LON Application - Production Deployment Guide
## Deployment на elon.elbosoft.click

---

## 📋 Преглед

Ова е чекор-по-чекор упатство за deployment на LON апликацијата на Contabo VPS серверот.

**Детали:**
- Домен: `elon.elbosoft.click`
- Сервер IP: `173.212.254.216`
- Локација: `/opt/apps/LON/LON-test`
- Web сервер: Caddy (со автоматски SSL)
- Контејнери: SQL Server, API, Worker, Frontend

---

## 🚀 Deployment Чекори

### Чекор 1: Поврзи се на серверот

```bash
ssh root@173.212.254.216
cd /opt/apps/LON/LON-test
```

---

### Чекор 2: Backup на постоечките фајлови (опционално)

```bash
# Backup на docker-compose.yml
cp docker-compose.yml docker-compose.yml.backup

# Backup на Caddyfile (ако постои во овој директориум)
cp Caddyfile Caddyfile.backup 2>/dev/null || true
```

---

### Чекор 3: Замени ги Dockerfile фајловите

**За API:**
```bash
# Отвори го фајлот за уредување
nano src/LON.API/Dockerfile
```

Замени ја целата содржина со:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["LON.sln", "./"]
COPY ["src/LON.API/LON.API.csproj", "src/LON.API/"]
COPY ["src/LON.Application/LON.Application.csproj", "src/LON.Application/"]
COPY ["src/LON.Domain/LON.Domain.csproj", "src/LON.Domain/"]
COPY ["src/LON.Infrastructure/LON.Infrastructure.csproj", "src/LON.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/LON.API/LON.API.csproj"

# Copy everything else
COPY . .

# Build and publish
WORKDIR "/src/src/LON.API"
RUN dotnet publish "LON.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install sqlcmd for healthcheck
RUN apt-get update && apt-get install -y curl gnupg && \
    curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - && \
    curl https://packages.microsoft.com/config/debian/11/prod.list > /etc/apt/sources.list.d/mssql-release.list && \
    apt-get update && \
    ACCEPT_EULA=Y apt-get install -y mssql-tools18 unixodbc-dev && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=build /app/publish .

# Expose port
EXPOSE 5000

# Set entry point
ENTRYPOINT ["dotnet", "LON.API.dll"]
```

Зачувај: `CTRL+X`, потоа `Y`, потоа `ENTER`

---

**За Worker:**
```bash
nano src/LON.Worker/Dockerfile
```

Замени ја целата содржина со:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["LON.sln", "./"]
COPY ["src/LON.Worker/LON.Worker.csproj", "src/LON.Worker/"]
COPY ["src/LON.Application/LON.Application.csproj", "src/LON.Application/"]
COPY ["src/LON.Domain/LON.Domain.csproj", "src/LON.Domain/"]
COPY ["src/LON.Infrastructure/LON.Infrastructure.csproj", "src/LON.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/LON.Worker/LON.Worker.csproj"

# Copy everything else
COPY . .

# Build and publish
WORKDIR "/src/src/LON.Worker"
RUN dotnet publish "LON.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published files
COPY --from=build /app/publish .

# Set entry point
ENTRYPOINT ["dotnet", "LON.Worker.dll"]
```

Зачувај: `CTRL+X`, потоа `Y`, потоа `ENTER`

---

### Чекор 4: Креирај го .env фајлот

```bash
nano .env
```

Копирај ја оваа содржина (со генерираните сигурни passwords):

```env
# LON Production Environment Variables
# ВАЖНО: Чувај го овој фајл сигурно и НИКОГАШ не го commit-увај во Git!

# SQL Server Configuration
SQL_SA_PASSWORD=GPXRjf3T02jr5bmZoKOCYgnYPEqaFjYwAa1!

# JWT Authentication
JWT_SECRET_KEY=SRRhp8bdb75UwfDbG7oQMgBNPKDhmaFIPCTnMHyWBj1FYRNmiycFu23lt44K0VQ9

# OpenAI Configuration (опционално - остави празно ако не користиш)
OPENAI_API_KEY=
ENABLE_VECTOR_STORE=false

# Application Settings
ASPNETCORE_ENVIRONMENT=Production
```

Зачувај: `CTRL+X`, потоа `Y`, потоа `ENTER`

---

### Чекор 5: Замени го docker-compose.yml

```bash
nano docker-compose.yml
```

Замени ја целата содржина со:

```yaml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: lon-sqlserver
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=${SQL_SA_PASSWORD}
      - MSSQL_PID=Developer
    ports:
      - "1433:1433"
    volumes:
      - sqlserver_data:/var/opt/mssql
    networks:
      - lon-network
      - web
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '${SQL_SA_PASSWORD}' -C -Q 'SELECT 1' || exit 1
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s
    restart: unless-stopped

  api:
    build:
      context: .
      dockerfile: src/LON.API/Dockerfile
    container_name: lon-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5000
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=LONDB;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=True;
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
      - JwtSettings__Issuer=LON.API
      - JwtSettings__Audience=LON.Client
      - JwtSettings__ExpiryMinutes=60
      - OpenAI__ApiKey=${OPENAI_API_KEY:-}
      - OpenAI__EmbeddingModel=text-embedding-ada-002
      - OpenAI__ChatModel=gpt-4o-mini
      - EnableVectorStore=${ENABLE_VECTOR_STORE:-false}
    expose:
      - "5000"
    depends_on:
      sqlserver:
        condition: service_healthy
    networks:
      - lon-network
      - web
    restart: unless-stopped

  worker:
    build:
      context: .
      dockerfile: src/LON.Worker/Dockerfile
    container_name: lon-worker
    environment:
      - DOTNET_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=LONDB;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=True;
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
      - OpenAI__ApiKey=${OPENAI_API_KEY:-}
      - OpenAI__EmbeddingModel=text-embedding-ada-002
      - OpenAI__ChatModel=gpt-4o-mini
      - EnableVectorStore=${ENABLE_VECTOR_STORE:-false}
    depends_on:
      sqlserver:
        condition: service_healthy
      api:
        condition: service_started
    networks:
      - lon-network
    restart: unless-stopped

  frontend:
    build:
      context: ./frontend/web
      dockerfile: Dockerfile
    container_name: lon-frontend
    expose:
      - "80"
    depends_on:
      - api
    networks:
      - lon-network
      - web
    restart: unless-stopped

volumes:
  sqlserver_data:

networks:
  lon-network:
    driver: bridge
  web:
    external: true
```

Зачувај: `CTRL+X`, потоа `Y`, потоа `ENTER`

---

### Чекор 6: Ажурирај го Caddyfile

Caddyfile обично е на локација `/etc/caddy/Caddyfile` или во home директориумот.

```bash
# Најди го Caddyfile
find /etc /opt /root -name "Caddyfile" 2>/dev/null

# Отвори го за уредување (замени го патот ако е друг)
nano /etc/caddy/Caddyfile
```

**Додај ја оваа конфигурација** (остави ја постоечката за inventory!):

```
# ---- LON app ----
elon.elbosoft.click {
  
  # API routes
  @api path /api* /swagger* /health
  handle @api {
    reverse_proxy lon-api:5000
  }

  # Frontend routes
  handle {
    reverse_proxy lon-frontend:80
  }
}
```

Зачувај и reload-ни го Caddy:

```bash
# Тестирај дали Caddyfile е валиден
caddy validate --config /etc/caddy/Caddyfile

# Reload на Caddy (ако работи како systemd service)
systemctl reload caddy

# ИЛИ ако работи во Docker
docker restart caddy
```

---

### Чекор 7: Креирај deployment скрипта

```bash
nano deploy.sh
```

Копирај ја содржината од `deploy.sh` фајлот (пратен одделно).

Зачувај и направи ја извршна:

```bash
chmod +x deploy.sh
```

---

### Чекор 8: Стартувај го deployment процесот

```bash
./deploy.sh
```

Оваа скрипта ќе:
1. Проверит дали `.env` фајлот постои
2. Стопирање на постоечки контејнери
3. Градење на Docker слики
4. Стартување на сервиси
5. Чекање на SQL Server
6. Прикажување на статус

---

### Чекор 9: Провери дали сè работи

**Проверка на контејнери:**
```bash
docker ps
```

Треба да видиш:
- `lon-sqlserver`
- `lon-api`
- `lon-worker`
- `lon-frontend`

**Проверка на логови:**
```bash
# API логови
docker logs -f lon-api

# Frontend логови
docker logs -f lon-frontend

# SQL Server логови
docker logs -f lon-sqlserver

# Worker логови
docker logs -f lon-worker
```

**Тестирај во browser:**
- Frontend: `https://elon.elbosoft.click`
- API Health: `https://elon.elbosoft.click/api/health`
- API Swagger: `https://elon.elbosoft.click/swagger`

---

## 🔧 Troubleshooting

### Ако SQL Server не работи:

```bash
# Провери логови
docker logs lon-sqlserver

# Провери дали порт 1433 е слободен
netstat -tulpn | grep 1433

# Рестартирај контејнер
docker restart lon-sqlserver
```

### Ако API не работи:

```bash
# Провери логови
docker logs lon-api

# Провери environment variables
docker exec lon-api env | grep -E "ConnectionStrings|JwtSettings"

# Рестартирај
docker restart lon-api
```

### Ако Frontend не работи:

```bash
# Провери логови
docker logs lon-frontend

# Проверка nginx config
docker exec lon-frontend cat /etc/nginx/conf.d/default.conf

# Рестартирај
docker restart lon-frontend
```

### Ако Caddy не работи:

```bash
# Проверка Caddy статус
systemctl status caddy

# Провери Caddy логови
journalctl -u caddy -f

# Тестирај config
caddy validate --config /etc/caddy/Caddyfile

# Рестартирај Caddy
systemctl restart caddy
```

### Rebuild на контејнери (ако направиш промени):

```bash
cd /opt/apps/LON/LON-test
docker-compose down
docker-compose build --no-cache
docker-compose up -d
```

---

## 📊 Корисни команди

```bash
# Статус на сите контејнери
docker ps -a

# Стопирај сè
docker-compose down

# Стопирај и избриши volumes (ВНИМАНИЕ: губи податоци!)
docker-compose down -v

# Стартувај сè одново
docker-compose up -d

# Проверка на мрежа
docker network inspect web
docker network inspect lon-network

# Restart на одреден контејнер
docker restart lon-api

# Влез во контејнер (за debugging)
docker exec -it lon-api bash
docker exec -it lon-sqlserver bash

# Проверка на disk space
df -h
docker system df

# Чистење на неискористени ресурси
docker system prune -a
```

---

## 🔐 Сигурност

1. **Никогаш не commit-увај `.env` фајл во Git**
2. **Редовно backup на SQL Server база:**
   ```bash
   docker exec lon-sqlserver /opt/mssql-tools18/bin/sqlcmd \
     -S localhost -U sa -P 'PASSWORD' -C \
     -Q "BACKUP DATABASE LONDB TO DISK='/var/opt/mssql/backup/LONDB.bak'"
   ```
3. **Промени ги passwords периодично**
4. **Користи firewall за заштита на портови**

---

## 📝 Забелешки

- SQL Server податоците се чуваат во Docker volume `sqlserver_data`
- Caddy автоматски генерира SSL сертификати преку Let's Encrypt
- Сите контејнери автоматски се рестартираат при грешка (`restart: unless-stopped`)
- Frontend е build-иран со npm и служен преку nginx
- API работи на .NET 8
- Worker е background service за обработка на евенти

---

## 📞 Поддршка

Ако имаш проблеми, провери:
1. Docker логови (`docker logs <container>`)
2. Caddy логови (`journalctl -u caddy`)
3. System ресурси (`htop`, `df -h`)
4. Мрежа конфигурација (`docker network inspect web`)

---

**Успешен deployment! 🎉**
