using EduPlatform.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Data;
using EduPlatform.Models;
using System.Data;
using System.Text.Json;
using System.Security.Cryptography;

namespace EduPlatform.Controllers
{
    /// <summary>
    /// Контролер проходження тестування.
    ///
    /// Основні можливості:
    /// 1. Створення нової спроби тестування.
    /// 2. Перевірка права студента на проходження тесту.
    /// 3. Контроль максимальної кількості спроб.
    /// 4. Випадкове перемішування питань для нової спроби.
    /// 5. Збереження порядку питань у базі даних.
    /// 6. Перевірка та оцінювання відповідей на сервері.
    /// 7. Збереження результатів і оновлення прогресу.
    /// 8. Контроль доступу до результатів тестування.
    /// </summary>
    public class TestController : BaseController
    {
        /// <summary>
        /// Конструктор контролера.
        /// Отримує контекст бази даних через механізм
        /// впровадження залежностей ASP.NET Core.
        /// </summary>
        public TestController(ApplicationDbContext context)
            : base(context)
        {
        }

        // ============================================================
        // 1. СТВОРЕННЯ НОВОЇ СПРОБИ ТЕСТУВАННЯ
        // ============================================================

        /// <summary>
        /// Створює нову спробу проходження тесту.
        ///
        /// Метод викликається після натискання кнопки
        /// «Пройти тест» на сторінці навчального курсу.
        ///
        /// Під час створення спроби:
        /// - перевіряється авторизація користувача;
        /// - перевіряється роль студента;
        /// - перевіряється запис студента на курс;
        /// - перевіряється наявність питань;
        /// - перевіряється ліміт спроб;
        /// - генерується випадковий порядок питань;
        /// - порядок зберігається в базі даних.
        ///
        /// Якщо незавершена спроба вже існує,
        /// студент продовжує її замість створення нової.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int moduleId, bool practice = false)
        {
            // Отримуємо ідентифікатор користувача із сесії.
            var userId = HttpContext.Session.GetInt32("UserId");

            // Неавторизованого користувача перенаправляємо
            // на сторінку входу до системи.
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Проходити тестування може лише студент.
            if (HttpContext.Session.GetString("UserRole") != "Student")
            {
                return Forbid();
            }

            // Транзакція об'єднує перевірку та створення спроби.
            // Для SQLite це не замінює додаткових обмежень
            // унікальності, які варто передбачити окремо.
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            // Завантажуємо модуль разом із питаннями.
            var module = await _context.Modules
                .Include(m => m.Questions)
                .FirstOrDefaultAsync(m => m.Id == moduleId);

            // Якщо модуль не існує, повертаємо HTTP 404.
            if (module == null)
            {
                return NotFound();
            }

            if (module.IsArchived)
            {
                TempData["Error"] =
                    "Цей тест архівовано. Нові спроби недоступні.";

                return RedirectToAction(
                    "Details",
                    "Course",
                    new { id = module.CourseId });
            }

            // Перевіряємо, чи записаний студент на курс,
            // до якого належить вибраний модуль.
            var enrolled = await _context.Enrollments
                .AnyAsync(e =>
                    e.StudentId == userId.Value &&
                    e.CourseId == module.CourseId);

            // Забороняємо проходження тестів чужих курсів.
            if (!enrolled)
            {
                return Forbid();
            }

            // Не створюємо спробу для порожнього тесту.
            if (module.Questions.Count == 0)
            {
                TempData["Error"] =
                    "У цьому тесті ще немає питань.";

                return RedirectToAction(
                    "Details",
                    "Course",
                    new { id = module.CourseId });
            }

            // Самоперевірка доступна тільки після завершення ВСЬОГО курсу:
            // кожний активний тест має бути успішно складений (>= 60%).
            // Окрім цього, звичайні спроби саме цього тесту мають бути вичерпані.
            // Перевірка на сервері не дозволяє обійти правило прямим POST-запитом.
            if (practice)
            {
                var activeModuleIds = await _context.Modules
                    .Where(m => m.CourseId == module.CourseId && !m.IsArchived)
                    .Select(m => m.Id)
                    .ToListAsync();

                var passedModuleIds = await _context.Attempts
                    .Where(a => a.StudentId == userId.Value &&
                                !a.IsPractice && a.FinishedAt != null && a.Score >= 60 &&
                                activeModuleIds.Contains(a.ModuleId))
                    .Select(a => a.ModuleId)
                    .Distinct()
                    .ToListAsync();

                bool courseCompleted = activeModuleIds.Count > 0 &&
                    activeModuleIds.All(id => passedModuleIds.Contains(id));

                // Рахуємо завершені спроби та одне зарезервоване місце
                // для будь-якої кількості незавершених звичайних спроб.
                int finishedOrdinaryAttempts = await _context.Attempts.CountAsync(a =>
                    a.StudentId == userId.Value && a.ModuleId == moduleId &&
                    !a.IsPractice && a.FinishedAt != null);

                bool hasActiveOrdinaryAttempt = await _context.Attempts.AnyAsync(a =>
                    a.StudentId == userId.Value && a.ModuleId == moduleId &&
                    !a.IsPractice && a.FinishedAt == null);

                bool ordinaryAttemptsExhausted = module.MaxAttempts > 0 &&
                    finishedOrdinaryAttempts >= module.MaxAttempts &&
                    !hasActiveOrdinaryAttempt;

                if (!courseCompleted || !ordinaryAttemptsExhausted)
                {
                    TempData["Error"] = !courseCompleted
                        ? "Самоперевірка доступна лише після завершення всього курсу."
                        : "Спочатку використайте доступні звичайні спроби тесту.";
                    return RedirectToAction("Details", "Course", new { id = module.CourseId });
                }
            }

            // Успішне складання тесту не скасовує залишок звичайних спроб.
            // Збільшення MaxAttempts викладачем відкриває додаткові спроби.

            // Шукаємо незавершену спробу цього студента.
            // Повторне натискання кнопки не повинно
            // автоматично створювати нову спробу.
            var activeAttempt = await _context.Attempts
                .Where(a =>
                    a.StudentId == userId.Value &&
                    a.ModuleId == moduleId &&
                    a.IsPractice == practice &&
                    a.FinishedAt == null)
                .OrderBy(a => a.Id)
                .FirstOrDefaultAsync();

            if (activeAttempt != null)
            {
                // Для старих спроб, створених до додавання
                // механізму збереження порядку, генеруємо
                // порядок один раз та записуємо його в базу.
                if (string.IsNullOrWhiteSpace(
                    activeAttempt.QuestionOrderJson))
                {
                    var questionIds = module.Questions
                        .Select(q => q.Id);

                    activeAttempt.QuestionOrderJson =
                        JsonSerializer.Serialize(
                            ShuffleQuestionIds(questionIds));

                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                // Продовжуємо вже існуючу спробу.
                return RedirectToAction(
                    nameof(Take),
                    new { attemptId = activeAttempt.Id });
            }

            // Підраховуємо лише завершені звичайні спроби.
            // Незавершені вже перевірено вище: їх продовжуємо, а не
            // створюємо нові, тому вони не дублюють витрачений ліміт.
            var usedAttempts = await _context.Attempts
                .CountAsync(a =>
                    a.StudentId == userId.Value &&
                    a.ModuleId == moduleId && !a.IsPractice &&
                    a.FinishedAt != null);

            // Якщо MaxAttempts > 0, діє обмеження.
            // Значення 0 у цій реалізації означає,
            // що кількість спроб не обмежена.
            if (!practice && module.MaxAttempts > 0 &&
                usedAttempts >= module.MaxAttempts)
            {
                TempData["Error"] =
                    "Вичерпано дозволену кількість спроб тестування.";

                return RedirectToAction(
                    "Details",
                    "Course",
                    new { id = module.CourseId });
            }

            // Отримуємо ідентифікатори питань модуля.
            var ids = module.Questions.Select(q => q.Id);

            // Генеруємо випадкову послідовність питань.
            var randomOrder = ShuffleQuestionIds(ids);

            // Створюємо нову спробу тестування.
            var attempt = new Attempt
            {
                StudentId = userId.Value,
                ModuleId = moduleId,
                StartedAt = DateTime.Now,
                IsPractice = practice,

                // Зберігаємо послідовність ідентифікаторів
                // у форматі JSON.
                //
                // Наприклад: [12, 5, 9, 3].
                QuestionOrderJson =
                    JsonSerializer.Serialize(randomOrder)
            };

            // Додаємо спробу до контексту бази даних.
            _context.Attempts.Add(attempt);

            // Зберігаємо спробу разом із порядком питань.
            await _context.SaveChangesAsync();

            // Підтверджуємо транзакцію.
            await transaction.CommitAsync();

            // Відкриваємо сторінку проходження тесту.
            return RedirectToAction(
                nameof(Take),
                new { attemptId = attempt.Id });
        }

        // ============================================================
        // 2. ВИПАДКОВЕ ПЕРЕМІШУВАННЯ ПИТАНЬ
        // ============================================================

        /// <summary>
        /// Перемішує ідентифікатори питань за алгоритмом
        /// Фішера–Єйтса.
        ///
        /// Алгоритм забезпечує випадкову перестановку
        /// елементів списку.
        ///
        /// Перемішування виконується під час створення спроби,
        /// а не при кожному відкритті сторінки.
        /// </summary>
        private static List<int> ShuffleQuestionIds(
            IEnumerable<int> ids)
        {
            // Створюємо окремий список ідентифікаторів.
            var result = ids.ToList();

            // Послідовно перемішуємо елементи списку.
            for (var i = result.Count - 1; i > 0; i--)
            {
                // Генеруємо випадковий індекс від 0 до i.
                var j = RandomNumberGenerator.GetInt32(i + 1);

                // Міняємо місцями два елементи.
                (result[i], result[j]) =
                    (result[j], result[i]);
            }

            // Повертаємо випадкову послідовність.
            return result;
        }

        // ============================================================
        // 3. ВІДНОВЛЕННЯ ЗБЕРЕЖЕНОГО ПОРЯДКУ ПИТАНЬ
        // ============================================================

        /// <summary>
        /// Відновлює порядок питань із поля QuestionOrderJson.
        ///
        /// Порядок залишається стабільним після оновлення
        /// сторінки та повторного відкриття спроби.
        ///
        /// Якщо склад питань змінився, видалені питання
        /// ігноруються, а нові додаються в кінець списку.
        ///
        /// Для повної незмінності тесту після старту
        /// додатково потрібно заборонити редагування питань
        /// активної спроби або зберігати їхню версію.
        /// </summary>
        private static List<int> GetQuestionOrder(Attempt attempt)
        {
            // Отримуємо актуальні ідентифікатори питань.
            var currentIds = attempt.Module!.Questions
                .Select(q => q.Id)
                .ToHashSet();

            List<int> savedIds;

            try
            {
                // Перетворюємо JSON на список ідентифікаторів.
                savedIds = JsonSerializer.Deserialize<List<int>>(
                    attempt.QuestionOrderJson ?? "[]")
                    ?? new List<int>();
            }
            catch (JsonException)
            {
                // Якщо JSON пошкоджений, використовуємо
                // порожній список ідентифікаторів.
                savedIds = new List<int>();
            }

            // Залишаємо лише актуальні питання,
            // прибираючи можливі повторення.
            var orderedIds = savedIds
                .Where(currentIds.Contains)
                .Distinct()
                .ToList();

            // Додаємо питання, які відсутні у збереженому списку.
            orderedIds.AddRange(
                currentIds
                    .Except(orderedIds)
                    .OrderBy(id => id));

            return orderedIds;
        }

        // ============================================================
        // 4. СТОРІНКА ПРОХОДЖЕННЯ ТЕСТУ
        // ============================================================

        /// <summary>
        /// Відкриває сторінку проходження тестування.
        ///
        /// Перевіряє належність спроби студентові
        /// та передає представленням збережений порядок питань.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Take(int attemptId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (HttpContext.Session.GetString("UserRole") != "Student")
            {
                return Forbid();
            }

            // Завантажуємо спробу, модуль, питання та відповіді.
            var attempt = await _context.Attempts
                .Include(a => a.Module)!
                    .ThenInclude(m => m.Questions)
                    .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(a => a.Id == attemptId);

            if (attempt == null)
            {
                return NotFound();
            }

            // Студент не може відкривати чужу спробу.
            if (attempt.StudentId != userId.Value)
            {
                return Forbid();
            }

            // Якщо спробу вже завершено, відкриваємо результат.
            if (attempt.FinishedAt != null)
            {
                return RedirectToAction(
                    nameof(Result),
                    new { attemptId });
            }

            // Відновлюємо збережену послідовність питань.
            ViewBag.QuestionOrder = GetQuestionOrder(attempt);

            // Передаємо спробу до Razor-представлення.
            return View(attempt);
        }

        // ============================================================
        // 5. ПЕРЕВІРКА ТА ЗБЕРЕЖЕННЯ ВІДПОВІДЕЙ
        // ============================================================

        /// <summary>
        /// Приймає відповіді студента та обчислює результат.
        ///
        /// Усі перевірки виконуються на сервері.
        /// Клієнтська перевірка не вважається достатньою.
        ///
        /// Формат відповідей відповідає наявній формі:
        /// Dictionary<int, List<string>>.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(
            int attemptId,
            Dictionary<int, List<string>>? answers)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (HttpContext.Session.GetString("UserRole") != "Student")
            {
                return Forbid();
            }

            // Використовуємо транзакцію для збереження
            // результату та оновлення навчального прогресу.
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            // Завантажуємо спробу з питаннями та відповідями.
            var attempt = await _context.Attempts
                .Include(a => a.Module)!
                    .ThenInclude(m => m.Questions)
                    .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(a => a.Id == attemptId);

            if (attempt == null)
            {
                return NotFound();
            }

            // Забороняємо змінювати чужі спроби.
            if (attempt.StudentId != userId.Value)
            {
                return Forbid();
            }

            // Завершену спробу не оцінюємо повторно.
            if (attempt.FinishedAt != null)
            {
                return RedirectToAction(
                    nameof(Result),
                    new { attemptId });
            }

            if (attempt.Module == null)
            {
                return NotFound();
            }

            // Отримуємо всі питання модуля.
            var questions = attempt.Module.Questions.ToList();

            if (questions.Count == 0)
            {
                TempData["Error"] =
                    "У цьому тесті ще немає питань.";

                return RedirectToAction(
                    nameof(Take),
                    new { attemptId });
            }

            // Якщо відповіді не передані, створюємо
            // порожній словник для подальшої перевірки.
            answers ??= new Dictionary<int, List<string>>();

            // Формуємо множину допустимих ідентифікаторів питань.
            var validQuestionIds = questions
                .Select(q => q.Id)
                .ToHashSet();

            // Забороняємо надсилання відповідей на питання,
            // яких немає в поточному модулі.
            if (answers.Keys.Any(id =>
                !validQuestionIds.Contains(id)))
            {
                return BadRequest(
                    "Запит містить невідомі питання.");
            }

            // Перевіряємо заповнення кожного питання.
            foreach (var question in questions)
            {
                if (!answers.TryGetValue(question.Id, out var selected) ||
                    selected == null ||
                    selected.Count == 0 ||
                    selected.All(string.IsNullOrWhiteSpace))
                {
                    TempData["Error"] =
                        "Будь ласка, дайте відповідь на всі питання.";

                    TempData["SelectedAnswersJson"] =
                        JsonSerializer.Serialize(answers);

                    return RedirectToAction(
                        nameof(Take),
                        new { attemptId });
                }
            }

            // Перевіряємо, що всі надіслані варіанти дійсно
            // належать відповідним питанням. Не приймаємо
            // порожні значення та дублікати.
            foreach (var question in questions)
            {
                var selected = answers[question.Id];
                var allowedOptions = question.Answers
                    .Select(a => a.Text)
                    .ToHashSet(StringComparer.Ordinal);

                if (selected.Any(string.IsNullOrWhiteSpace) ||
                    selected.Count != selected.Distinct(StringComparer.Ordinal).Count() ||
                    selected.Any(value => !allowedOptions.Contains(value)))
                {
                    return BadRequest(
                        "Запит містить некоректні варіанти відповідей.");
                }
            }

            // Кількість правильно виконаних питань.
            var correctCount = 0;

            // Оцінюємо кожне питання окремо.
            foreach (var question in questions)
            {
                var selected = answers[question.Id];

                // Створюємо множину вибраних відповідей.
                var selectedSet = selected
                    .ToHashSet(StringComparer.Ordinal);

                // Отримуємо правильні варіанти з бази даних.
                var correctSet = question.Answers
                    .Where(a => a.IsCorrect)
                    .Select(a => a.Text)
                    .ToHashSet(StringComparer.Ordinal);

                // Відповідь правильна лише тоді, коли:
                // - існує хоча б один правильний варіант;
                // - немає повторених значень;
                // - вибрано всі правильні варіанти;
                // - не вибрано жодного неправильного.
                var isCorrect = AnswerEvaluator.IsCorrect(selected, correctSet);

                if (isCorrect)
                {
                    correctCount++;
                }

                // Зберігаємо відповідь на питання.
                _context.AttemptAnswers.Add(
                    new AttemptAnswer
                    {
                        AttemptId = attempt.Id,
                        QuestionId = question.Id,
                        SelectedAnswer = string.Join(", ", selected),
                        IsCorrect = isCorrect
                    });
            }

            // Обчислюємо результат у відсотках.
            attempt.Score =
                correctCount * 100 / questions.Count;

            // Фіксуємо час завершення спроби.
            attempt.FinishedAt = DateTime.Now;

            // Самоперевірка зберігає результат, але не змінює навчальний прогрес.
            if (!attempt.IsPractice)
            {
                // Шукаємо навчальний прогрес студента.
                var progress = await _context.Progresses
                    .FirstOrDefaultAsync(p =>
                        p.StudentId == attempt.StudentId &&
                        p.CourseId == attempt.Module.CourseId);

                if (progress == null)
                {
                    // Створюємо перший запис прогресу.
                    _context.Progresses.Add(new Progress
                    {
                        StudentId = attempt.StudentId,
                        CourseId = attempt.Module.CourseId,
                        CompletedTests = 1,
                        BestScore = attempt.Score,
                        LastAttempt = attempt.FinishedAt.Value
                    });
                }
                else
                {
                    // Збільшуємо кількість завершених тестів.
                    progress.CompletedTests++;

                    // Оновлюємо найкращий результат.
                    progress.BestScore = Math.Max(
                        progress.BestScore,
                        attempt.Score);

                    // Фіксуємо дату останнього тестування.
                    progress.LastAttempt =
                        attempt.FinishedAt.Value;
                }

            }

            // Зберігаємо всі зміни.
            await _context.SaveChangesAsync();

            // Підтверджуємо транзакцію.
            await transaction.CommitAsync();

            // Відкриваємо сторінку результатів.
            return RedirectToAction(
                nameof(Result),
                new { attemptId = attempt.Id });
        }

        // ============================================================
        // 6. ПЕРЕГЛЯД РЕЗУЛЬТАТІВ ТЕСТУВАННЯ
        // ============================================================

        /// <summary>
        /// Відображає результат завершеної спроби.
        ///
        /// Студент може переглядати власні результати.
        /// Викладач може переглядати результати тестів
        /// лише тих курсів, автором яких він є.
        ///
        /// Дозвіл на показ правильних відповідей
        /// визначається сервером.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Result(int attemptId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Завантажуємо спробу, курс, питання
            // та збережені відповіді студента.
            var attempt = await _context.Attempts
                .Include(a => a.Module)!
                    .ThenInclude(m => m.Course)
                .Include(a => a.Module)!
                    .ThenInclude(m => m.Questions)
                    .ThenInclude(q => q.Answers)
                .Include(a => a.AttemptAnswers)
                .FirstOrDefaultAsync(a => a.Id == attemptId);

            if (attempt == null)
            {
                return NotFound();
            }

            // Визначаємо роль поточного користувача.
            var role = HttpContext.Session.GetString("UserRole");

            // Студент має доступ до власної спроби.
            var ownsAttempt =
                role == "Student" &&
                attempt.StudentId == userId.Value;

            // Викладач має доступ до результатів
            // лише власного навчального курсу.
            var ownsCourse =
                role == "Teacher" &&
                attempt.Module?.Course?.AuthorId == userId.Value;

            // Забороняємо доступ стороннім користувачам.
            if (!ownsAttempt && !ownsCourse)
            {
                return Forbid();
            }

            // Не показуємо результат незавершеної спроби.
            if (attempt.FinishedAt == null)
            {
                if (ownsAttempt)
                {
                    return RedirectToAction(
                        nameof(Take),
                        new { attemptId });
                }

                return NotFound();
            }

            // Викладач може переглядати правильні відповіді.
            // Для студента це залежить від налаштування курсу.
            //
            // Не довіряємо параметрам URL, оскільки користувач
            // може змінити їх вручну.
            ViewBag.ShowReview =
                ownsCourse ||
                (attempt.Module?.Course?.ShowCorrectAnswers ?? false);

            return View(attempt);
        }
    }
}
