# LON Application - Deployment Package

## 📦 Содржина на пакетот

Овој пакет содржи сè што ти треба за deployment на LON апликацијата на production серверот.

### Фајлови:

1. **DEPLOYMENT_GUIDE.md** - Детално упатство со сите чекори
2. **QUICK_REFERENCE.md** - Брзо упатство за чести команди
3. **docker-compose.production.yml** - Production-ready Docker Compose конфигурација
4. **Dockerfile.API** - Dockerfile за LON.API
5. **Dockerfile.Worker** - Dockerfile за LON.Worker
6. **Caddyfile.updated** - Ажурирана Caddy конфигурација
7. **.env.production** - Environment variables со генерирани сигурни passwords
8. **.env.example** - Template за .env фајл
9. **.gitignore** - За да не се commit-уваат sensitive фајлови
10. **deploy.sh** - Автоматска deployment скрипта

---

## 🚀 Брз старт

### Чекор 1: Трансфер на фајлови на серверот

```bash
# На твојот локален компјутер
scp -r LON-deployment-package root@173.212.254.216:/tmp/
```

### Чекор 2: Setup на серверот

```bash
# Поврзи се на серверот
ssh root@173.212.254.216

# Оди во проектот
cd /opt/apps/LON/LON-test

# Копирај ги фајловите
cp /tmp/LON-deployment-package/docker-compose.production.yml ./docker-compose.yml
cp /tmp/LON-deployment-package/.env.production ./.env
cp /tmp/LON-deployment-package/deploy.sh ./
cp /tmp/LON-deployment-package/Dockerfile.API ./src/LON.API/Dockerfile
cp /tmp/LON-deployment-package/Dockerfile.Worker ./src/LON.Worker/Dockerfile
cp /tmp/LON-deployment-package/.gitignore ./

# Прави deploy скриптата извршна
chmod +x deploy.sh
```

### Чекор 3: Ажурирај Caddyfile

```bash
# Најди ја локацијата на Caddyfile
find /etc /opt /root -name "Caddyfile" 2>/dev/null

# Отвори го Caddyfile (обично е на /etc/caddy/Caddyfile)
nano /etc/caddy/Caddyfile

# Додај ја содржината од Caddyfile.updated (пази: не бришај постоечка конфигурација!)

# Reload на Caddy
systemctl reload caddy
```

### Чекор 4: Deploy!

```bash
cd /opt/apps/LON/LON-test
./deploy.sh
```

---

## 📖 Детални инструкции

Отвори го **DEPLOYMENT_GUIDE.md** за чекор-по-чекор упатство.

---

## ⚙️ Што е генерирано за тебе:

### 🔐 Сигурни Passwords (генерирани автоматски):

- **SQL Server Password:** `GPXRjf3T02jr5bmZoKOCYgnYPEqaFjYwAa1!`
- **JWT Secret Key:** `SRRhp8bdb75UwfDbG7oQMgBNPKDhmaFIPCTnMHyWBj1FYRNmiycFu23lt44K0VQ9`

⚠️ **ВАЖНО:** Овие passwords се генерирани со `openssl` и се криптографски сигурни. Чувај ги на сигурно место!

---

## 🌐 URL адреси (по deployment):

- Frontend: https://elon.elbosoft.click
- API: https://elon.elbosoft.click/api
- Health Check: https://elon.elbosoft.click/api/health
- Swagger: https://elon.elbosoft.click/swagger

---

## 🔧 Архитектура

```
Internet
   ↓
DNS (elon.elbosoft.click) → 173.212.254.216
   ↓
Caddy (Port 443, HTTPS + SSL auto-renewal)
   ↓
Docker Network: web
   ↓
   ├─→ lon-frontend:80 (React/Nginx)
   └─→ lon-api:5000 (.NET 8 API)
        ↓
   Docker Network: lon-network
        ↓
        ├─→ lon-sqlserver:1433 (SQL Server 2022)
        └─→ lon-worker (Background Worker)
```

---

## 📝 Забелешки

1. **SQL Server податоци** се перзистентни (чувани во Docker volume)
2. **Автоматски SSL сертификат** преку Let's Encrypt
3. **Restart policy** е поставен на `unless-stopped`
4. **Health checks** се конфигурирани за SQL Server
5. **Secrets** се во `.env` фајл (не се hardcoded)

---

## 🆘 Поддршка

Ако имаш проблеми:
1. Погледни во **DEPLOYMENT_GUIDE.md** → Troubleshooting секција
2. Погледни во **QUICK_REFERENCE.md** → 🆘 Проблеми? секција
3. Провери логови: `docker logs -f lon-api`

---

## ✅ Checklist пред deployment:

- [ ] Фајловите се копирани на серверот
- [ ] `.env` фајлот е креиран
- [ ] Dockerfile-овите се на место
- [ ] Caddyfile е ажуриран
- [ ] DNS record е конфигуриран (`elon.elbosoft.click` → `173.212.254.216`)
- [ ] Docker network `web` постои
- [ ] `deploy.sh` е извршен (`chmod +x`)

---

**Среќен deployment! 🎉**

За било какви прашања или проблеми, јави се!
