# User Management System - Имплементација

## Преглед

Креиран е комплетен User Management систем со следните модули:

## Модули

### 1. **Authentication (Најава)**
- **Login страница** (`/login`)
  - Најава со username и password
  - JWT token автентикација
  - Автоматско пренасочување на dashboard

### 2. **User Management (Управување со корисници)**
- **Патека**: `/admin/users`
- **Функции**:
  - Листа на сите корисници
  - Креирање нов корисник
  - Измена на постоечки корисник
  - Бришење корисник
  - Доделување улоги на корисници
  - Статус (Активен/Неактивен)

### 3. **Employee Management (Вработени)**
- **Патека**: `/admin/employees`
- **Функции**:
  - Листа на вработени
  - Креирање вработен (поврзан со корисник)
  - Измена на податоци за вработен
  - Бришење вработен
  - Податоци: име, презиме, email, телефон, позиција, одделение, датум на вработување

### 4. **Shift Management (Смени)**
- **Патека**: `/admin/shifts`
- **Функции**:
  - Листа на работни смени
  - Креирање нова смена
  - Измена на смена
  - Бришење смена
  - Дефинирање на време (почеток/крај)
  - Автоматско пресметување на траење

### 5. **Role Management (Улоги и дозволи)**
- **Патека**: `/admin/roles`
- **Функции**:
  - Листа на улоги
  - Креирање нова улога
  - Измена на улога
  - Бришење улога
  - Доделување дозволи на улога
  - Преглед на дозволи по ресурс

## Креирани фајлови

### Services
- `/frontend/web/src/services/authService.ts` - Authentication и User Management API
- `/frontend/web/src/services/employeeService.ts` - Employee и Shift Management API

### Pages
- `/frontend/web/src/pages/Login.tsx` + `.css` - Login страница
- `/frontend/web/src/pages/UserManagement.tsx` + `.css` - Управување со корисници
- `/frontend/web/src/pages/EmployeeManagement.tsx` + `.css` - Управување со вработени
- `/frontend/web/src/pages/ShiftManagement.tsx` + `.css` - Управување со смени
- `/frontend/web/src/pages/RoleManagement.tsx` + `.css` - Управување со улоги
- `/frontend/web/src/pages/Dashboard.tsx` (ажуриран) - Dashboard со линкови до модулите

### App Routes
- Ажуриран `/frontend/web/src/App.tsx` со:
  - Protected routes
  - Authentication провера
  - Routing за сите User Management модули

## Backend API Endpoints

### Authentication
- `POST /api/auth/login` - Најава
- `GET /api/users/current` - Тековен корисник

### Users
- `GET /api/users` - Листа на корисници
- `GET /api/users/{id}` - Еден корисник
- `POST /api/users` - Креирај корисник
- `PUT /api/users/{id}` - Ажурирај корисник
- `DELETE /api/users/{id}` - Избриши корисник
- `POST /api/users/{id}/change-password` - Промени лозинка

### Employees
- `GET /api/employees` - Листа на вработени
- `GET /api/employees/{id}` - Еден вработен
- `POST /api/employees` - Креирај вработен
- `PUT /api/employees/{id}` - Ажурирај вработен
- `DELETE /api/employees/{id}` - Избриши вработен

### Shifts
- `GET /api/shifts` - Листа на смени
- `GET /api/shifts/{id}` - Една смена
- `POST /api/shifts` - Креирај смена
- `PUT /api/shifts/{id}` - Ажурирај смена
- `DELETE /api/shifts/{id}` - Избриши смена

### Roles & Permissions
- `GET /api/roles` - Листа на улоги
- `GET /api/roles/{id}` - Една улога
- `POST /api/roles` - Креирај улога
- `PUT /api/roles/{id}` - Ажурирај улога
- `DELETE /api/roles/{id}` - Избриши улога
- `GET /api/permissions` - Листа на дозволи

## Seed податоци

Backend содржи seed податоци:
- Admin корисник: `admin` / `Admin123!`
- Улоги: Admin, Warehouse Manager, Production Manager, Customs Officer
- Дозволи за секоја улога
- Примерок вработени и смени

## Testing

### 1. Стартување
```bash
# Backend
docker-compose up -d

# Frontend
cd frontend/web
npm start
```

### 2. Тест сценарио
1. Отвори `http://localhost:3000`
2. Автоматски ќе те редиректира на `/login`
3. Најави се со: `admin` / `Admin123!`
4. Dashboard ќе покаже модули за администрација
5. Тестирај секој модул:
   - Корисници - Додади/Измени/Избриши
   - Вработени - Поврзи со корисник
   - Смени - Креирај работни смени
   - Улоги - Управувај со дозволи

## Следни чекори

1. ✅ Backend API - Комплетирано
2. ✅ Frontend UI - Комплетирано
3. ⏳ Testing - Во тек
4. ⏳ Integration testing
5. ⏳ Permission checking во UI
6. ⏳ Employee-Shift assignment UI

## Напомени

- Сите страни се responsive (mobile-friendly)
- Користи македонски јазик за UI
- Модерен дизајн со градиенти и анимации
- Protected routes - потребна најава
- JWT token автентикација
- LocalStorage за session management
