using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Data;
using EduPlatform.Models;

namespace EduPlatform.Controllers
{
    public class StudentController : BaseController
    {
        // Мінімальний бал для успішного проходження тесту.
        private const int PassingScore = 60;

        public StudentController(ApplicationDbContext context) : base(context)
        {
        }

        // GET: Кабінет студента
        [HttpGet]
        public async Task<IActionResult> Dashboard()
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

            // Завантажуємо лише курси, на які записаний поточний студент.
            var enrolledCourses = await _context.Courses
                .AsNoTracking()
                .Include(c => c.Modules)
                .Where(c => _context.Enrollments.Any(e =>
                    e.CourseId == c.Id && e.StudentId == userId.Value))
                .OrderBy(c => c.Title)
                .ToListAsync();

            // Для статистики враховуємо лише завершені спроби.
            var finishedAttempts = await _context.Attempts
                .AsNoTracking()
                .Where(a => a.StudentId == userId.Value && a.FinishedAt != null)
                .Select(a => new { a.ModuleId, a.Score })
                .ToListAsync();

            var completedTests = finishedAttempts.Count;
            var avgScore = completedTests > 0
                ? Math.Round(finishedAttempts.Average(a => (double)a.Score), 1)
                : 0.0;

            // Для проходження модуля достатньо однієї успішної спроби.
            // Повторні успішні спроби не збільшують прогрес двічі.
            var passedModuleIds = finishedAttempts
                .Where(a => a.Score >= PassingScore)
                .Select(a => a.ModuleId)
                .ToHashSet();

            var courseProgress = new Dictionary<int, int>();
            var completedCourses = 0;

            foreach (var course in enrolledCourses)
            {
                // Архівовані тести залишаються в історії результатів,
                // але не входять до поточної програми курсу.
                var moduleIds = course.Modules
                    .Where(m => !m.IsArchived)
                    .Select(m => m.Id)
                    .Distinct()
                    .ToList();

                var totalModules = moduleIds.Count;
                var passedModulesCount = moduleIds.Count(passedModuleIds.Contains);

                // Курс без активних тестів не вважаємо завершеним.
                var percent = totalModules == 0
                    ? 0
                    : (int)Math.Round(100.0 * passedModulesCount / totalModules);

                courseProgress[course.Id] = percent;

                if (totalModules > 0 && passedModulesCount == totalModules)
                {
                    completedCourses++;
                }
            }

            // Зберігаємо назви ViewBag, які використовують існуючі Razor Views.
            ViewBag.EnrolledCourses = enrolledCourses;
            ViewBag.CompletedTests = completedTests;
            ViewBag.AvgScore = avgScore;
            ViewBag.CompletedCourses = completedCourses;
            ViewBag.CourseProgress = courseProgress;

            return View();
        }

        // GET: Історія завершених тестувань поточного студента
        [HttpGet]
        public async Task<IActionResult> MyProgress()
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

            // Історія містить завершені спроби, зокрема архівованих тестів.
            var attempts = await _context.Attempts
                .AsNoTracking()
                .Include(a => a.Module)
                    .ThenInclude(m => m!.Course)
                .Where(a => a.StudentId == userId.Value && a.FinishedAt != null)
                .OrderByDescending(a => a.FinishedAt)
                .ToListAsync();

            return View(attempts);
        }

        // GET: Курси, на які записаний поточний студент
        [HttpGet]
        public async Task<IActionResult> MyCourses()
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

            var enrolledCourses = await _context.Courses
                .AsNoTracking()
                .Include(c => c.Modules)
                .Where(c => _context.Enrollments.Any(e =>
                    e.CourseId == c.Id && e.StudentId == userId.Value))
                .OrderBy(c => c.Title)
                .ToListAsync();

            return View(enrolledCourses);
        }
    }
}
