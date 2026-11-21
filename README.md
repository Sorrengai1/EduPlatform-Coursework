# EduPlatform

авчальна вебплатформа для керування курсами, навчальними матеріалами та тестуванням студентів.

## Технології
- ASP.NET Core MVC (.NET 10)
- Entity Framework Core
- SQLite
- Playwright (E2E-тестування)

## Вимоги
- .NET SDK 10
- Node.js та npm (для E2E-тестів)

## Запуск застосунку
1. ідкрити термінал у кореневому каталозі репозиторію.
2. иконати `dotnet restore EduPlatform.slnx`.
3. иконати `dotnet build EduPlatform.slnx`.
4. иконати `dotnet run --project EduPlatform/EduPlatform.csproj --launch-profile http`.
5. ідкрити http://localhost:5225.

Під час першого запуску база даних SQLite створюється автоматично за допомогою `EnsureCreated()`. айли робочої бази даних не зберігаються в Git-репозиторії.

## E2E-тестування
1. ерейти до каталогу `e2e-tests`.
2. иконати `npm ci`.
3. иконати `npx playwright install chromium`.
4. апустити вебзастосунок за наведеною вище інструкцією.
5. иконати `npx playwright test`.

Для окремих тестів з авторизацією необхідно локально задати змінні середовища з обліковими даними тестових користувачів. е зберігайте паролі в репозиторії.

## Структура
- `EduPlatform/` — вихідний код вебзастосунку.
- `e2e-tests/` — автоматизовані E2E-тести.
- `EduPlatform.slnx` — файл рішення .NET.

## Конфігурація
ядок підключення до SQLite задається параметром `ConnectionStrings:DefaultConnection` у `EduPlatform/appsettings.json`.
