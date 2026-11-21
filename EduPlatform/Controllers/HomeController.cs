
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Data;
using EduPlatform.Models;

namespace EduPlatform.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApplicationDbContext context,
            ILogger<HomeController> logger) : base(context)
        {
            _logger = logger;
        }

        // GET: Головна сторінка
        public async Task<IActionResult> Index()
        {
            // Три публічні курси з найбільшою кількістю студентів.
            var popularCourses = await _context.Courses
                .AsNoTracking()
                .Where(c => c.IsPublic)
                .OrderByDescending(c => c.Enrollments
                    .Select(e => e.StudentId)
                    .Distinct()
                    .Count())
                .ThenBy(c => c.Title)
                .Take(3)
                .Include(c => c.Modules)
                .Include(c => c.Author)
                .ToListAsync();

            // Кількість записаних студентів для кожного курсу.
            var courseIds = popularCourses
                .Select(c => c.Id)
                .ToList();

            var enrollmentCounts = await _context.Enrollments
                .AsNoTracking()
                .Where(e => courseIds.Contains(e.CourseId))
                .GroupBy(e => e.CourseId)
                .Select(g => new
                {
                    CourseId = g.Key,
                    Count = g.Select(e => e.StudentId).Distinct().Count()
                })
                .ToDictionaryAsync(x => x.CourseId, x => x.Count);

            ViewBag.EnrollmentCounts = enrollmentCounts;

            // Якщо записів ще немає, показуємо рекомендовані курси.
            ViewBag.HasPopularCourses =
                enrollmentCounts.Values.Any(count => count > 0);

            // Реальна статистика платформи.
            var courseCount = await _context.Courses.CountAsync();
            var moduleCount = await _context.Modules.CountAsync();
            var questionCount = await _context.Questions.CountAsync();

            ViewBag.TotalCoursesAndModules = courseCount + moduleCount;
            ViewBag.TotalQuestions = questionCount;

            return View(popularCourses);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id
                    ?? HttpContext.TraceIdentifier
            });
        }
    }
}
