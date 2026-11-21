using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Data;
using EduPlatform.Models;

namespace EduPlatform.Controllers
{
    public class CourseController : BaseController
    {
        private const long MaxImageSize = 5 * 1024 * 1024;

        public CourseController(ApplicationDbContext context) : base(context)
        {
        }

        // GET: Каталог курсів
        public async Task<IActionResult> Index()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetInt32("UserId");

            IQueryable<Course> coursesQuery = _context.Courses
                .AsNoTracking()
                .Include(c => c.Modules);

            if (userRole == "Teacher" && userId.HasValue)
            {
                coursesQuery = coursesQuery.Where(c => c.IsPublic || c.AuthorId == userId.Value);
            }
            else if (userRole == "Student" && userId.HasValue)
            {
                coursesQuery = coursesQuery.Where(c =>
                    c.IsPublic || c.Enrollments.Any(e => e.StudentId == userId.Value));
            }
            else
            {
                coursesQuery = coursesQuery.Where(c => c.IsPublic);
            }

            var courses = await coursesQuery.ToListAsync();
            return View(courses);
        }

        // GET: Сторінка курсу
        public async Task<IActionResult> Details(int id)
        {
            var course = await _context.Courses
                .AsNoTracking()
                .Include(c => c.Modules)
                .ThenInclude(m => m.Questions)
                .Include(c => c.Materials)
                .Include(c => c.Enrollments)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();

            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            bool isAuthor = userRole == "Teacher" && userId.HasValue &&
                            course.AuthorId == userId.Value;
            bool isEnrolled = userRole == "Student" && userId.HasValue &&
                              course.Enrollments.Any(e => e.StudentId == userId.Value);

            if (!course.IsPublic && !isAuthor && !isEnrolled)
                return NotFound();

            // Результати звичайних (не тренувальних) спроб поточного студента.
            var passedModuleIds = new HashSet<int>();
            var usedAttemptsByModule = new Dictionary<int, int>();
            var activeAttemptModuleIds = new HashSet<int>();
            if (isEnrolled && userId.HasValue)
            {
                var ids = await _context.Attempts.AsNoTracking()
                    .Where(a => a.StudentId == userId.Value &&
                                a.FinishedAt != null && !a.IsPractice &&
                                a.Score >= 60 && a.Module != null &&
                                a.Module.CourseId == course.Id && !a.Module.IsArchived)
                    .Select(a => a.ModuleId)
                    .Distinct()
                    .ToListAsync();
                passedModuleIds = ids.ToHashSet();

                // Завершені звичайні спроби витрачають ліміт назавжди.
                // Незавершена спроба резервує лише ОДНЕ місце незалежно від
                // кількості старих незавершених записів у базі.
                var attemptsByModule = await _context.Attempts.AsNoTracking()
                    .Where(a => a.StudentId == userId.Value && !a.IsPractice &&
                                a.Module != null && a.Module.CourseId == course.Id)
                    .GroupBy(a => a.ModuleId)
                    .Select(g => new
                    {
                        ModuleId = g.Key,
                        FinishedCount = g.Count(a => a.FinishedAt != null),
                        HasActive = g.Any(a => a.FinishedAt == null)
                    })
                    .ToListAsync();

                usedAttemptsByModule = attemptsByModule.ToDictionary(
                    x => x.ModuleId,
                    x => x.FinishedCount + (x.HasActive ? 1 : 0));

                // Незавершену звичайну спробу студент продовжує.
                activeAttemptModuleIds = attemptsByModule
                    .Where(x => x.HasActive)
                    .Select(x => x.ModuleId)
                    .ToHashSet();
            }
            ViewBag.PassedModuleIds = passedModuleIds;
            ViewBag.UsedAttemptsByModule = usedAttemptsByModule;
            ViewBag.ActiveAttemptModuleIds = activeAttemptModuleIds;
            var activeModules = course.Modules.Where(m => !m.IsArchived).ToList();
            ViewBag.IsCourseCompleted = isEnrolled && activeModules.Count > 0 &&
                activeModules.All(m => passedModuleIds.Contains(m.Id));

            ViewBag.IsEnrolled = isEnrolled;
            ViewBag.Teacher = course.AuthorId.HasValue
                ? await _context.Students.AsNoTracking().FirstOrDefaultAsync(s =>
                    s.Id == course.AuthorId.Value && s.Role == "Teacher")
                : null;

            return View(course);
        }

        // GET: Створення курсу
        public IActionResult Create()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Account");
            if (HttpContext.Session.GetString("UserRole") != "Teacher") return Forbid();
            return View(new Course { IsPublic = true });
        }

        // POST: Створення курсу
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Course course, IFormFile? imageFile)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (!currentUserId.HasValue) return RedirectToAction("Login", "Account");
            if (HttpContext.Session.GetString("UserRole") != "Teacher") return Forbid();

            // Ідентифікатор автора береться тільки із сесії.
            course.AuthorId = currentUserId.Value;
            ModelState.Remove(nameof(Course.AuthorId));

            if (string.IsNullOrWhiteSpace(course.Title))
                ModelState.AddModelError(nameof(Course.Title), "Вкажіть назву курсу.");
            if (string.IsNullOrWhiteSpace(course.Description))
                ModelState.AddModelError(nameof(Course.Description), "Вкажіть опис курсу.");

            // Перевіряємо формат, розмір і сигнатуру зображення.
            // Ключ imageFile збігається з name поля у формі.
            string? imageExtension = null;
            if (imageFile != null)
            {
                imageExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

                if (!allowedExtensions.Contains(imageExtension))
                {
                    ModelState.AddModelError("imageFile",
                        "Непідтримуваний формат зображення. Дозволено JPG, PNG та WEBP.");
                }
                else if (imageFile.Length == 0)
                {
                    ModelState.AddModelError("imageFile",
                        "Вибраний файл порожній. Оберіть інше зображення.");
                }
                else if (imageFile.Length > MaxImageSize)
                {
                    ModelState.AddModelError("imageFile",
                        "Розмір зображення не повинен перевищувати 5 МБ.");
                }
                else if (!await HasValidImageSignatureAsync(imageFile, imageExtension))
                {
                    ModelState.AddModelError("imageFile",
                        "Вміст файлу не відповідає формату зображення. Оберіть справжній файл JPG, PNG або WEBP.");
                }
            }

            if (!ModelState.IsValid) return View(course);

            // Створюємо сутність лише з дозволених полів форми.
            var newCourse = new Course
            {
                Title = course.Title.Trim(),
                Description = course.Description.Trim(),
                Level = course.Level,
                DurationHours = course.DurationHours,
                IsPublic = course.IsPublic,
                ShowCorrectAnswers = course.ShowCorrectAnswers,
                AuthorId = currentUserId.Value
            };

            string? savedFilePath = null;
            if (imageFile != null && imageFile.Length > 0 && imageExtension != null)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsFolder);

                // Не використовуємо ім'я файлу, отримане від користувача.
                var fileName = $"{Guid.NewGuid():N}{imageExtension}";
                savedFilePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(savedFilePath, FileMode.CreateNew))
                {
                    await imageFile.CopyToAsync(stream);
                }

                newCourse.ImagePath = "/uploads/" + fileName;
            }

            try
            {
                _context.Courses.Add(newCourse);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Якщо запис до БД не вдався, не залишаємо зайвий файл.
                if (savedFilePath != null && System.IO.File.Exists(savedFilePath))
                    System.IO.File.Delete(savedFilePath);
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Видалення власного курсу
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (!currentUserId.HasValue) return RedirectToAction("Login", "Account");
            if (HttpContext.Session.GetString("UserRole") != "Teacher") return Forbid();

            var course = await _context.Courses
                .Include(c => c.Modules).ThenInclude(m => m.Questions)
                .Include(c => c.Materials)
                .Include(c => c.Enrollments)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == id && c.AuthorId == currentUserId.Value);

            if (course == null) return NotFound();

            // Не видаляємо курс, якщо є історія проходження тестів.
            // Інакше каскадне видалення може знищити результати студентів.
            var hasAttempts = await _context.Attempts.AnyAsync(a =>
                a.Module != null && a.Module.CourseId == id);

            if (hasAttempts)
            {
                TempData["Error"] =
                    "Курс має історію тестувань студентів. Видалення заборонено, щоб зберегти результати.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Без перевірки конфігурації каскадного видалення та інших
            // пов'язаних сутностей безпечно видаляємо лише порожній курс.
            // Це також захищає записи студентів, матеріали та модулі.
            if (course.Modules.Any() || course.Materials.Any() || course.Enrollments.Any() ||
                await _context.Progresses.AnyAsync(p => p.CourseId == id))
            {
                TempData["Error"] =
                    "Курс містить модулі, матеріали, записи студентів або прогрес. " +
                    "Спочатку необхідно безпечно опрацювати пов'язані дані.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Порожній курс успішно видалено.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Самостійний запис студента на публічний курс
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(int courseId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Account");
            if (HttpContext.Session.GetString("UserRole") != "Student") return Forbid();

            var course = await _context.Courses.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == courseId);
            if (course == null) return NotFound();
            if (!course.IsPublic) return Forbid();

            bool alreadyEnrolled = await _context.Enrollments.AnyAsync(e =>
                e.CourseId == courseId && e.StudentId == userId.Value);

            if (!alreadyEnrolled)
            {
                _context.Enrollments.Add(new Enrollment
                {
                    CourseId = courseId,
                    StudentId = userId.Value
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = courseId });
        }

        private static async Task<bool> HasValidImageSignatureAsync(IFormFile file, string extension)
        {
            var header = new byte[12];
            using var stream = file.OpenReadStream();
            var read = 0;
            while (read < header.Length)
            {
                var count = await stream.ReadAsync(header.AsMemory(read, header.Length - read));
                if (count == 0) break;
                read += count;
            }

            if (extension == ".jpg" || extension == ".jpeg")
                return read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            if (extension == ".png")
                return read >= 8 && header.AsSpan(0, 8).SequenceEqual(
                    new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            if (extension == ".webp")
                return read >= 12 &&
                    header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                    header.AsSpan(8, 4).SequenceEqual("WEBP"u8);

            return false;
        }
    }
}
