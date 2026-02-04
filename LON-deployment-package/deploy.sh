#!/bin/bash

# LON Deployment Script
# Автор: Claude
# Датум: $(date +%Y-%m-%d)

set -e  # Стопира скриптата при грешка

echo "========================================="
echo "LON Application Deployment Script"
echo "========================================="
echo ""

# Боја кодови
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Директориум на проектот
PROJECT_DIR="/opt/apps/LON/LON-test"

# Функција за печатење со боја
print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${YELLOW}ℹ $1${NC}"
}

# Провери дали .env фајлот постои
if [ ! -f "$PROJECT_DIR/.env" ]; then
    print_error ".env фајлот не постои!"
    print_info "Креирај го .env фајлот пред deployment."
    exit 1
fi

print_success ".env фајлот постои"

# Оди во директориумот на проектот
cd "$PROJECT_DIR" || exit 1
print_success "Во директориумот: $PROJECT_DIR"

# Стопирај постоечки контејнери
print_info "Стопирам постоечки контејнери..."
docker-compose down 2>/dev/null || true
print_success "Контејнери стопирани"

# Изгради ги сликите
print_info "Градење на Docker слики..."
docker-compose build --no-cache
print_success "Слики изградени"

# Стартувај ги сервисите
print_info "Стартување на сервиси..."
docker-compose up -d
print_success "Сервиси стартувани"

# Чекај SQL Server да биде подготвен
print_info "Чекам SQL Server да биде подготвен (може да трае до 60 секунди)..."
timeout=60
counter=0
while ! docker exec lon-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SQL_SA_PASSWORD" -C -Q "SELECT 1" &> /dev/null; do
    sleep 2
    counter=$((counter+2))
    if [ $counter -ge $timeout ]; then
        print_error "SQL Server не се стартува. Проверете логови со: docker logs lon-sqlserver"
        exit 1
    fi
    echo -n "."
done
echo ""
print_success "SQL Server е подготвен!"

# Провери дали сите контејнери работат
print_info "Проверка на статус на контејнери..."
echo ""
docker-compose ps
echo ""

# Провери логови
print_info "Последни логови од API:"
docker logs --tail 20 lon-api
echo ""

print_success "========================================="
print_success "Deployment завршен!"
print_success "========================================="
echo ""
print_info "Апликацијата треба да биде достапна на:"
print_info "Frontend: https://elon.elbosoft.click"
print_info "API: https://elon.elbosoft.click/api"
echo ""
print_info "За да видиш логови:"
echo "  docker logs -f lon-api        # API логови"
echo "  docker logs -f lon-frontend   # Frontend логови"
echo "  docker logs -f lon-worker     # Worker логови"
echo "  docker logs -f lon-sqlserver  # SQL Server логови"
echo ""
print_info "За restart на контејнери:"
echo "  docker-compose restart"
echo ""
