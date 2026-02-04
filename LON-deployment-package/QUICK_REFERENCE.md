# LON Deployment - Брзо упатство (Quick Reference)

## 🚀 Брз старт (ако се фајловите веќе на серверот)

```bash
# 1. Поврзи се на серверот
ssh root@173.212.254.216

# 2. Оди во директориумот
cd /opt/apps/LON/LON-test

# 3. Стартувај deployment
./deploy.sh
```

---

## 📦 Прв пат setup (од нула)

```bash
# 1. Поврзи се на серверот
ssh root@173.212.254.216
cd /opt/apps/LON/LON-test

# 2. Креирај .env фајл
nano .env
# (копирај содржина од .env.production)

# 3. Замени docker-compose.yml
nano docker-compose.yml
# (копирај нова верзија)

# 4. Ажурирај Dockerfile-ови
nano src/LON.API/Dockerfile
nano src/LON.Worker/Dockerfile

# 5. Ажурирај Caddyfile
nano /etc/caddy/Caddyfile
# (додај LON конфигурација)
systemctl reload caddy

# 6. Deployment
./deploy.sh
```

---

## 🔧 Чести команди

### Restart на апликацијата
```bash
cd /opt/apps/LON/LON-test
docker-compose restart
```

### Проверка на логови
```bash
# API
docker logs -f lon-api

# Frontend
docker logs -f lon-frontend

# SQL Server
docker logs -f lon-sqlserver

# Worker
docker logs -f lon-worker

# Сите заедно
docker-compose logs -f
```

### Статус на контејнери
```bash
docker ps
docker-compose ps
```

### Rebuild (ако има промени во код)
```bash
cd /opt/apps/LON/LON-test
docker-compose down
docker-compose build --no-cache
docker-compose up -d
```

### Стопирање
```bash
docker-compose down
```

### Целосно чистење (ВНИМАНИЕ: губи податоци!)
```bash
docker-compose down -v
```

---

## 🌐 URL адреси

- **Frontend:** https://elon.elbosoft.click
- **API:** https://elon.elbosoft.click/api
- **Health check:** https://elon.elbosoft.click/api/health
- **Swagger:** https://elon.elbosoft.click/swagger

---

## 🔐 Важни локации

- **Проект:** `/opt/apps/LON/LON-test`
- **Caddyfile:** `/etc/caddy/Caddyfile`
- **Env фајл:** `/opt/apps/LON/LON-test/.env`
- **Docker compose:** `/opt/apps/LON/LON-test/docker-compose.yml`

---

## 🆘 Проблеми?

### SQL Server не работи
```bash
docker logs lon-sqlserver
docker restart lon-sqlserver
```

### API не работи
```bash
docker logs lon-api
docker exec lon-api env | grep ConnectionStrings
docker restart lon-api
```

### Frontend не работи
```bash
docker logs lon-frontend
docker restart lon-frontend
```

### Caddy проблеми
```bash
systemctl status caddy
journalctl -u caddy -f
systemctl restart caddy
```

---

## 📊 Monitoring

```bash
# Disk space
df -h
docker system df

# Processes
htop

# Network
docker network inspect web
docker network inspect lon-network

# Container resources
docker stats
```

---

## 🔄 Update процес

```bash
# 1. Backup (опционално)
cd /opt/apps/LON/LON-test
docker-compose down
tar -czf lon-backup-$(date +%Y%m%d).tar.gz .

# 2. Pull нов код (ако е во git)
git pull

# 3. Rebuild
docker-compose build --no-cache

# 4. Deploy
docker-compose up -d

# 5. Провери
docker-compose ps
docker logs -f lon-api
```

---

## 💾 Backup на база

```bash
# Креирај backup
docker exec lon-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'GPXRjf3T02jr5bmZoKOCYgnYPEqaFjYwAa1!' -C \
  -Q "BACKUP DATABASE LONDB TO DISK='/var/opt/mssql/backup/LONDB_$(date +%Y%m%d).bak'"

# Копирај backup надвор од контејнер
docker cp lon-sqlserver:/var/opt/mssql/backup/LONDB_$(date +%Y%m%d).bak .
```

---

**За детални инструкции, види: DEPLOYMENT_GUIDE.md**
