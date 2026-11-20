using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Data;
using EduPlatform.Models;

namespace EduPlatform.Controllers
{
    public class TeacherController : BaseController
    {
        public TeacherController(ApplicationDbContext context) : base(context)
        {
        }

        private bool IsTeacher()
        {
            return HttpContext.Session.GetString("UserRole") == "Teacher";
        }

        // Після першої спроби склад і відповіді тесту не змінюємо:
        // інакше історичні результати можуть втратити достовірність.
        private Task<bool> HasTestAttempts(int moduleId)
        {
            return _context.Attempts.AnyAsync(a => a.ModuleId == moduleId);
        }

        private IActionResult RedirectForLockedTest(int moduleId)
        {
            TempData["Error"] =
                "Питання тесту не можна змінювати після початку проходження " +
                "студентами. Для нової версії створіть окремий тест.";
            return RedirectToAction(nameof(ManageQuestions), new { moduleId });
        }

        // GET: Огляд (Dashboard)
        public async Task<IActionResult> Index()
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var currentUserId = HttpContext.Session.GetInt32("UserId");

            ViewBag.TotalCourses = await _context.Courses.CountAsync(c => c.AuthorId == currentUserId);
            ViewBag.TotalStudents = await _context.Enrollments
                .Where(e => e.Course!.AuthorId == currentUserId)
                .Select(e => e.StudentId).Distinct().CountAsync();
            ViewBag.TotalTests = await _context.Modules.CountAsync(m => m.Course!.AuthorId == currentUserId);
            ViewBag.TotalResults = await _context.Attempts.CountAsync(a => a.Module!.Course!.AuthorId == currentUserId && a.FinishedAt != null);

            ViewBag.RecentResults = await _context.Attempts
                .Include(a => a.Student).Include(a => a.Module)
                .Where(a => a.Module!.Course!.AuthorId == currentUserId && a.FinishedAt != null)
                .OrderByDescending(a => a.StartedAt).Take(5).ToListAsync();

            ViewBag.Students = await _context.Enrollments
                .Where(e => e.Course!.AuthorId == currentUserId)
                .Select(e => e.Student).Distinct().ToListAsync();

            return View();
        }

        // GET: Мої курси
        public async Task<IActionResult> MyCourses()
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var currentUserId = HttpContext.Session.GetInt32("UserId");

            var createdCourses = await _context.Courses
                .Include(c => c.Modules)
                .Include(c => c.Enrollments)
                .Where(c => c.AuthorId == currentUserId)
                .ToListAsync();

            var enrolledCourseIds = await _context.Enrollments
                .Where(e => e.StudentId == currentUserId)
                .Select(e => e.CourseId)
                .ToListAsync();

            var enrolledCourses = await _context.Courses
                .Include(c => c.Modules)
                .Include(c => c.Enrollments)
                .Where(c => enrolledCourseIds.Contains(c.Id))
                .ToListAsync();

            ViewBag.CreatedCourses = createdCourses;
            ViewBag.EnrolledCourses = enrolledCourses;

            return View();
        }

        // GET: Мої тести
        public async Task<IActionResult> MyTests()
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var currentUserId = HttpContext.Session.GetInt32("UserId");

            var tests = await _context.Modules
                .Include(m => m.Course)
                .Include(m => m.Questions)
                .Where(m => m.Course!.AuthorId == currentUserId)
                .ToListAsync();

            return View(tests);
        }

        // POST: Видалення тесту або його архівування
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTest(int id)
        {
            var teacherId = HttpContext.Session.GetInt32("UserId");

            if (!IsTeacher() || !teacherId.HasValue)
            {
                return Forbid();
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            var module = await _context.Modules
                .Include(m => m.Course)
                .Include(m => m.Questions)
                    .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (module == null)
            {
                return NotFound();
            }

            // Викладач може керувати лише тестами власних курсів
            if (module.Course?.AuthorId != teacherId.Value)
            {
                return Forbid();
            }

            // Не дозволяємо видаляти тест з активними спробами
            bool hasActiveAttempts = await _context.Attempts
                .AnyAsync(a =>
                    a.ModuleId == id &&
                    a.FinishedAt == null);

            if (hasActiveAttempts)
            {
                TempData["Error"] =
                    "Тест має незавершені спроби студентів. " +
                    "Дочекайтеся їх завершення.";

                return RedirectToAction(nameof(MyTests));
            }

            bool hasHistory = await _context.Attempts
                .AnyAsync(a => a.ModuleId == id);

            if (hasHistory)
            {
                // Зберігаємо результати студентів
                module.IsArchived = true;

                TempData["Success"] =
                    "Тест архівовано. Результати студентів збережено.";
            }
            else
            {
                // Тест ніхто не проходив: можна видалити
                _context.Answers.RemoveRange(
                    module.Questions.SelectMany(q => q.Answers));

                _context.Questions.RemoveRange(module.Questions);

                _context.Modules.Remove(module);

                TempData["Success"] =
                    "Тест та його питання успішно видалено.";
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(nameof(MyTests));
        }

        // POST: Відновлення архівованого тесту
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreTest(int id)
        {
            var teacherId = HttpContext.Session.GetInt32("UserId");

            if (!IsTeacher() || !teacherId.HasValue)
            {
                return Forbid();
            }

            var module = await _context.Modules
                .Include(m => m.Course)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (module == null)
            {
                return NotFound();
            }

            if (module.Course?.AuthorId != teacherId.Value)
            {
                return Forbid();
            }

            if (!module.IsArchived)
            {
                TempData["Error"] = "Цей тест уже активний.";
                return RedirectToAction(nameof(MyTests));
            }

            module.IsArchived = false;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Тест успішно відновлено.";

            return RedirectToAction(nameof(MyTests));
        }

        // GET: Створення тесту
        public async Task<IActionResult> CreateTest()
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            ViewBag.Courses = await _context.Courses.Where(c => c.AuthorId == currentUserId).ToListAsync();
            return View();
        }

        // POST: Створення тесту
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTest(int courseId, string title, int maxAttempts = 3, bool showCorrectAnswers = false)
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.AuthorId == currentUserId);
            if (course == null) return Forbid();

            if (string.IsNullOrWhiteSpace(title) || maxAttempts < 0)
            {
                TempData["Error"] = "Вкажіть назву тесту та допустиму кількість спроб.";
                return RedirectToAction(nameof(CreateTest));
            }

            var module = new CourseModule
            {
                CourseId = courseId,
                Title = title,
                MaxAttempts = maxAttempts
            };
            _context.Modules.Add(module);
            await _context.SaveChangesAsync();

            course.ShowCorrectAnswers = showCorrectAnswers;
            await _context.SaveChangesAsync();

            return RedirectToAction("ManageQuestions", new { moduleId = module.Id });
        }

        // GET: Керування питаннями
        public async Task<IActionResult> ManageQuestions(int moduleId)
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var module = await _context.Modules
                .Include(m => m.Course).Include(m => m.Questions).ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(m => m.Id == moduleId);

            if (module == null) return NotFound();

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (module.Course?.AuthorId != currentUserId) return Forbid();

            ViewBag.ModuleId = module.Id;
            ViewBag.ModuleTitle = module.Title;
            ViewBag.CourseTitle = module.Course?.Title;

            return View(module.Questions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMultipleQuestions(int moduleId, List<string> questionTexts, List<string> allOptions, string correctIndexesJson)
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var module = await _context.Modules.Include(m => m.Course)
                .FirstOrDefaultAsync(m => m.Id == moduleId);
            if (module == null) return NotFound();
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (module.Course?.AuthorId != currentUserId) return Forbid();
            if (module.IsArchived || await HasTestAttempts(moduleId))
                return RedirectForLockedTest(moduleId);

            List<List<int>>? indexes;
            try
            {
                indexes = System.Text.Json.JsonSerializer.Deserialize<List<List<int>>>(correctIndexesJson ?? "");
            }
            catch (System.Text.Json.JsonException)
            {
                indexes = null;
            }

            if (questionTexts == null || allOptions == null || indexes == null ||
                questionTexts.Count == 0 || questionTexts.Count != allOptions.Count ||
                questionTexts.Count != indexes.Count)
            {
                TempData["Error"] = "Некоректні дані питань. Перевірте всі поля та повторіть спробу.";
                return RedirectToAction(nameof(ManageQuestions), new { moduleId });
            }

            // Перевіряємо ВСІ питання до першого запису в базу.
            var prepared = new List<(string Text, List<string> Options, List<int> Correct)>();
            for (int i = 0; i < questionTexts.Count; i++)
            {
                var options = allOptions[i].Split('|').Select(x => x.Trim()).ToList();
                var correct = indexes[i];
                if (string.IsNullOrWhiteSpace(questionTexts[i]) || options.Count < 2 ||
                    options.Any(string.IsNullOrWhiteSpace) || correct == null ||
                    correct.Count == 0 || correct.Any(x => x < 0 || x >= options.Count) ||
                    correct.Distinct().Count() != correct.Count)
                {
                    TempData["Error"] = $"Питання {i + 1}: заповніть текст, щонайменше два варіанти та позначте хоча б одну правильну відповідь. Жодне питання не збережено.";
                    return RedirectToAction(nameof(ManageQuestions), new { moduleId });
                }
                prepared.Add((questionTexts[i].Trim(), options, correct));
            }

            foreach (var item in prepared)
            {
                var question = new Question
                {
                    Text = item.Text,
                    CorrectAnswer = string.Join(", ", item.Correct.Select(i => item.Options[i])),
                    ModuleId = moduleId,
                    Answers = item.Options.Select((option, i) => new Answer
                    {
                        Text = option,
                        IsCorrect = item.Correct.Contains(i)
                    }).ToList()
                };
                _context.Questions.Add(question);
            }
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Успішно збережено {prepared.Count} питань!";
            return RedirectToAction(nameof(ManageQuestions), new { moduleId });
        }

        // GET: Редагування питання
        public async Task<IActionResult> EditQuestion(int id)
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var question = await _context.Questions
                .Include(q => q.Answers).Include(q => q.Module).ThenInclude(m => m!.Course)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null) return NotFound();

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (question.Module?.Course?.AuthorId != currentUserId) return Forbid();
            if (await HasTestAttempts(question.ModuleId)) return RedirectForLockedTest(question.ModuleId);

            ViewBag.ModuleId = question.ModuleId;
            ViewBag.ModuleTitle = question.Module?.Title;
            return View(question);
        }

        // POST: Видалення питання
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var question = await _context.Questions
                .Include(q => q.Module)
                .ThenInclude(m => m!.Course)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null) return NotFound();

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (question.Module?.Course?.AuthorId != currentUserId) return Forbid();

            int moduleId = question.ModuleId;
            if (await HasTestAttempts(moduleId)) return RedirectForLockedTest(moduleId);

            _context.Questions.Remove(question);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Питання успішно видалено.";
            return RedirectToAction("ManageQuestions", new { moduleId = moduleId });
        }

        // POST: Збереження змін питання
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuestion(int id, string text, List<string> options, List<int> correctIndexes)
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var question = await _context.Questions
                .Include(q => q.Answers).Include(q => q.Module).ThenInclude(m => m!.Course)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null) return NotFound();

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (question.Module?.Course?.AuthorId != currentUserId) return Forbid();
            if (await HasTestAttempts(question.ModuleId)) return RedirectForLockedTest(question.ModuleId);

            if (string.IsNullOrWhiteSpace(text) ||
                options == null || options.Count < 2 ||
                options.Any(string.IsNullOrWhiteSpace) ||
                correctIndexes == null || correctIndexes.Count == 0 ||
                correctIndexes.Any(i => i < 0 || i >= options.Count))
            {
                ModelState.AddModelError("", "Заповніть текст питання, щонайменше два варіанти та позначте хоча б одну правильну відповідь.");
                ViewBag.ModuleId = question.ModuleId;
                ViewBag.ModuleTitle = question.Module?.Title;
                return View(question);
            }

            question.Text = text.Trim();
            var correctTexts = correctIndexes.Select(i => options[i]).ToList();
            question.CorrectAnswer = string.Join(", ", correctTexts);

            _context.Answers.RemoveRange(question.Answers);
            for (int i = 0; i < options.Count; i++)
            {
                _context.Answers.Add(new Answer
                {
                    Text = options[i],
                    IsCorrect = correctIndexes.Contains(i),
                    QuestionId = question.Id
                });
            }
            await _context.SaveChangesAsync();

            return RedirectToAction("ManageQuestions", new { moduleId = question.ModuleId });
        }

        // GET: Результати тесту
        public async Task<IActionResult> TestResults(int moduleId)
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");
            var module = await _context.Modules.Include(m => m.Course).FirstOrDefaultAsync(m => m.Id == moduleId);
            if (module == null) return NotFound();

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (module.Course?.AuthorId != currentUserId) return Forbid();

            var results = await _context.Attempts.Include(a => a.Student)
                .Where(a => a.ModuleId == moduleId && a.FinishedAt != null)
                .OrderByDescending(a => a.StartedAt).ToListAsync();

            ViewBag.Module = module;
            return View(results);
        }

        // GET: Налаштування модуля
        public async Task<IActionResult> EditSettings(int id)
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");
            var module = await _context.Modules.Include(m => m.Course).FirstOrDefaultAsync(m => m.Id == id);
            if (module == null) return NotFound();

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (module.Course?.AuthorId != currentUserId) return Forbid();

            return View(module);
        }

        // POST: Налаштування модуля
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSettings(int id, int maxAttempts, bool showCorrectAnswers)
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");
            var module = await _context.Modules.Include(m => m.Course).FirstOrDefaultAsync(m => m.Id == id);
            if (module == null) return NotFound();

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (module.Course?.AuthorId != currentUserId) return Forbid();

            if (maxAttempts < 0)
            {
                ModelState.AddModelError("", "Кількість спроб не може бути від’ємною.");
                return View(module);
            }

            module.MaxAttempts = maxAttempts;
            if (module.Course != null) module.Course.ShowCorrectAnswers = showCorrectAnswers;

            await _context.SaveChangesAsync();
            return RedirectToAction("MyTests");
        }


        // GET: Керування курсом
        public async Task<IActionResult> ManageCourse(int id, string? searchString)
        {
            if (!IsTeacher())
                return RedirectToAction("Index", "Home");

            var course = await _context.Courses
                .Include(c => c.Materials)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
                return NotFound();

            var currentUserId = HttpContext.Session.GetInt32("UserId");

            if (course.AuthorId != currentUserId)
                return Forbid();

            var enrolledIds = course.Enrollments
                .Select(e => e.StudentId)
                .ToList();

            var availableStudentsQuery = _context.Students
                .Where(s => s.Role == "Student" &&
                            !enrolledIds.Contains(s.Id));

            searchString = searchString?.Trim() ?? string.Empty;

            // Пошук виконується у C#, щоб коректно обробляти українські літери
            // незалежно від регістру (SQLite не завжди підтримує це для кирилиці).
            var availableStudents = await availableStudentsQuery.ToListAsync();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                availableStudents = availableStudents.Where(s =>
                {
                    var fullName = string.Join(" ", new[]
                    {
                        s.LastName,
                        s.Name,
                        s.MiddleName
                    }.Where(part => !string.IsNullOrWhiteSpace(part)));

                    return s.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase)
                        || (s.LastName?.Contains(searchString, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (s.MiddleName?.Contains(searchString, StringComparison.OrdinalIgnoreCase) ?? false)
                        || s.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase)
                        || fullName.Contains(searchString, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            ViewBag.AvailableStudents = availableStudents;
            ViewBag.SearchString = searchString;

            return View(course);
        }


        // POST: Запис студентів
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnrollStudent(int courseId, List<int> studentIds)
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.AuthorId == currentUserId);
            if (course == null) return Forbid();

            if (studentIds != null && studentIds.Count > 0)
            {
                // Додаємо лише реальних студентів; ігноруємо повторні ідентифікатори.
                var validStudentIds = await _context.Students
                    .Where(s => s.Role == "Student" && studentIds.Contains(s.Id))
                    .Select(s => s.Id)
                    .ToListAsync();

                var alreadyEnrolledIds = await _context.Enrollments
                    .Where(e => e.CourseId == courseId && validStudentIds.Contains(e.StudentId))
                    .Select(e => e.StudentId)
                    .ToListAsync();

                foreach (var studentId in validStudentIds.Except(alreadyEnrolledIds))
                {
                    _context.Enrollments.Add(new Enrollment
                    {
                        CourseId = courseId,
                        StudentId = studentId
                    });
                }

                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ManageCourse", new { id = courseId });
        }

        // POST: Додавання матеріалу
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMaterial(int courseId, string title, string? content, IFormFile? file)
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.AuthorId == currentUserId);
            if (course == null) return Forbid();

            title = title?.Trim() ?? string.Empty;
            content = content?.Trim();
            bool hasFile = file != null && file.Length > 0;

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "Вкажіть назву матеріалу.";
                return RedirectToAction(nameof(ManageCourse), new { id = courseId });
            }

            if (string.IsNullOrWhiteSpace(content) && !hasFile)
            {
                TempData["Error"] = "Додайте опис, посилання або прикріпіть файл.";
                return RedirectToAction(nameof(ManageCourse), new { id = courseId });
            }

            if (file != null)
            {
                const long maxFileSize = 5L * 1024 * 1024;
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!hasFile || (extension != ".pdf" && extension != ".docx"))
                {
                    TempData["Error"] = "Дозволено лише файли PDF та DOCX.";
                    return RedirectToAction(nameof(ManageCourse), new { id = courseId });
                }
                if (file.Length > maxFileSize)
                {
                    TempData["Error"] = "Розмір файлу не повинен перевищувати 5 МБ.";
                    return RedirectToAction(nameof(ManageCourse), new { id = courseId });
                }

                // Перевіряємо сигнатуру файлу, а не лише його розширення.
                var header = new byte[5];
                await using (var input = file.OpenReadStream())
                {
                    var read = await input.ReadAsync(header.AsMemory(0, header.Length));
                    bool validPdf = extension == ".pdf" && read >= 5 &&
                                    header[0] == 0x25 && header[1] == 0x50 &&
                                    header[2] == 0x44 && header[3] == 0x46 && header[4] == 0x2D;
                    bool validDocx = extension == ".docx" && read >= 4 &&
                                     header[0] == 0x50 && header[1] == 0x4B &&
                                     header[2] == 0x03 && header[3] == 0x04;
                    if (!validPdf && !validDocx)
                    {
                        TempData["Error"] = "Вміст файлу не відповідає формату PDF або DOCX.";
                        return RedirectToAction(nameof(ManageCourse), new { id = courseId });
                    }
                }
            }

            var material = new CourseMaterial { CourseId = courseId, Title = title, Content = content };

            if (hasFile)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file!.FileName).ToLowerInvariant();
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                await using (var stream = new FileStream(filePath, FileMode.CreateNew))
                {
                    await file.CopyToAsync(stream);
                }

                material.FilePath = "/uploads/" + uniqueFileName;
                material.Type = "Файл";
            }

            _context.Materials.Add(material);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Матеріал успішно додано.";
            return RedirectToAction("ManageCourse", new { id = courseId });
        }

        // POST: Видалення студента
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveStudent(int courseId, int studentId)
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.AuthorId == currentUserId);
            if (course == null) return Forbid();

            var enrollment = await _context.Enrollments.FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);
            if (enrollment != null) { _context.Enrollments.Remove(enrollment); await _context.SaveChangesAsync(); }
            return RedirectToAction("ManageCourse", new { id = courseId });
        }

        // POST: Видалення матеріалу
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMaterial(int id, int courseId)
        {
            if (!IsTeacher()) return Forbid();

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (!currentUserId.HasValue) return RedirectToAction("Login", "Account");

            // Перевіряємо одночасно матеріал, курс і його автора.
            var material = await _context.Materials.FirstOrDefaultAsync(m =>
                m.Id == id &&
                m.CourseId == courseId &&
                m.Course != null &&
                m.Course.AuthorId == currentUserId.Value);

            if (material == null) return NotFound();

            _context.Materials.Remove(material);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Матеріал успішно видалено.";
            return RedirectToAction("ManageCourse", new { id = courseId });
        }

        // GET: Мої матеріали
        public async Task<IActionResult> MyMaterials()
        {
            if (HttpContext.Session.GetString("UserRole") != "Teacher")
            {
                return RedirectToAction("Index", "Home");
            }

            int? teacherId = HttpContext.Session.GetInt32("UserId");

            if (teacherId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var materials = await _context.LibraryItems
                .AsNoTracking()
                .Where(m => m.AuthorId == teacherId.Value)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return View(materials);
        }

        // GET: Створення матеріалу
        public async Task<IActionResult> CreateMaterial()
        {
            if (!IsTeacher()) return RedirectToAction("Index", "Home");

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            ViewBag.Courses = await _context.Courses
                .Where(c => c.AuthorId == currentUserId)
                .ToListAsync();

            return View();
        }
    }
}