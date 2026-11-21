# EduPlatform

Навчальна вебплатформа для керування курсами, навчальними матеріалами та тестуванням студентів.

## Технології

- ASP.NET Core MVC (.NET 10)
- Entity Framework Core
- SQLite
- Playwright (E2E-тестування)

## Вимоги

- .NET SDK 10
- Node.js та npm (для E2E-тестування)

## Запуск застосунку

1. Відкрити термінал у кореневому каталозі репозиторію.
2. Виконати `dotnet restore EduPlatform.slnx`.
3. Виконати `dotnet build EduPlatform.slnx`.
4. Виконати `dotnet run --project EduPlatform/EduPlatform.csproj --launch-profile http`.
5. Відкрити вебзастосунок за адресою http://localhost:5225.

Під час першого запуску база даних SQLite створюється автоматично за допомогою методу `EnsureCreated()`. Файли робочої бази даних не зберігаються в Git-репозиторії.

## E2E-тестування

1. Перейти до каталогу `e2e-tests`.
2. Виконати `npm ci`.
3. Виконати `npx playwright install chromium`.
4. Запустити вебзастосунок за наведеною вище інструкцією.
5. Виконати `npx playwright test`.

Для окремих тестів з авторизацією необхідно локально задати змінні середовища з обліковими даними тестових користувачів. Не зберігайте паролі та інші секрети в репозиторії.

## Структура проєкту

- `EduPlatform/` — вихідний код вебзастосунку ASP.NET Core MVC.
- `EduPlatform/Migrations/` — файли міграцій Entity Framework Core.
- `e2e-tests/` — автоматизовані E2E-тести Playwright.
- `EduPlatform.slnx` — файл рішення .NET.
- `README.md` — інструкція зі встановлення та запуску проєкту.
- `.gitignore` — правила виключення службових і конфіденційних файлів із репозиторію.

## Конфігурація

Рядок підключення до бази даних SQLite задається параметром `ConnectionStrings:DefaultConnection` у файлі `EduPlatform/appsettings.json`.

За замовчуванням використовується:

`Data Source=eduplatform.db`

Налаштування запуску застосунку визначено у файлі `EduPlatform/Properties/launchSettings.json`.

Робочі бази даних, користувацькі завантаження, тимчасові файли та локальні секрети не включаються до Git-репозиторію.
