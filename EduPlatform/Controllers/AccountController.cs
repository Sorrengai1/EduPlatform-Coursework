using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Data;
using EduPlatform.Models;

namespace EduPlatform.Controllers
{
    public class AccountController : BaseController
    {
        private readonly PasswordHasher<Student> _passwordHasher = new();
        private const long MaxAvatarSize = 5 * 1024 * 1024; // 5 МБ

        public AccountController(ApplicationDbContext context) : base(context) { }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string? email, string? password)
        {
            email = (email ?? string.Empty).Trim();
            ViewBag.Email = email;

            // Не зберігаємо пароль у ModelState для повторного відображення.
            ModelState.Clear();
            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Введіть електронну адресу.");
            else if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
                ModelState.AddModelError("email", "Введіть коректну електронну адресу.");

            if (string.IsNullOrEmpty(password))
                ModelState.AddModelError("password", "Введіть пароль.");

            if (!ModelState.IsValid)
                return View();

            var user = await _context.Students
                .FirstOrDefaultAsync(s => s.Email.ToLower() == email.ToLower());

            if (user == null || string.IsNullOrWhiteSpace(user.Password))
            {
                ModelState.AddModelError("password", "Невірний email або пароль.");
                return View();
            }

            PasswordVerificationResult result;
            try
            {
                result = _passwordHasher.VerifyHashedPassword(user, user.Password, password!);
            }
            catch (FormatException)
            {
                result = PasswordVerificationResult.Failed;
            }

            if (result == PasswordVerificationResult.Failed ||
                (user.Role != "Student" && user.Role != "Teacher"))
            {
                ModelState.AddModelError("password", "Невірний email або пароль.");
                return View();
            }

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.Password = _passwordHasher.HashPassword(user, password!);
                await _context.SaveChangesAsync();
            }

            HttpContext.Session.Clear();
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetInt32("UserId", user.Id);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string? name, string? lastName, string? middleName,
            string? email, string? password, string? confirmPassword, string? role)
        {
            name = (name ?? string.Empty).Trim();
            lastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim();
            middleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim();
            email = (email ?? string.Empty).Trim();

            // Зберігаємо лише непарольні дані для повторного відображення форми.
            ViewBag.Name = name;
            ViewBag.LastName = lastName;
            ViewBag.MiddleName = middleName;
            ViewBag.Email = email;
            ViewBag.Role = role;

            // Валідація виконується на сервері незалежно від HTML-перевірок.
            if (name.Length == 0)
                ModelState.AddModelError("name", "Вкажіть ім'я.");
            else if (name.Length > 100)
                ModelState.AddModelError("name", "Ім'я не може перевищувати 100 символів.");

            if (string.IsNullOrWhiteSpace(lastName))
                ModelState.AddModelError("lastName", "Вкажіть прізвище.");
            else if (lastName.Length > 100)
                ModelState.AddModelError("lastName", "Прізвище не може перевищувати 100 символів.");

            if (middleName?.Length > 100)
                ModelState.AddModelError("middleName", "По батькові не може перевищувати 100 символів.");

            if (email.Length == 0)
                ModelState.AddModelError("email", "Вкажіть електронну адресу.");
            else if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
                ModelState.AddModelError("email", "Введіть коректну електронну адресу.");

            if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Length > 128)
                ModelState.AddModelError("password", "Пароль має містити від 8 до 128 символів.");

            if (string.IsNullOrEmpty(confirmPassword))
                ModelState.AddModelError("confirmPassword", "Підтвердіть пароль.");
            else if (password != confirmPassword)
                ModelState.AddModelError("confirmPassword", "Паролі не збігаються.");

            // Роль викладача обирається користувачем, як і в початковій версії.
            // Це не є незалежною перевіркою повноважень викладача.
            if (role != "Student" && role != "Teacher")
                ModelState.AddModelError("role", "Оберіть коректну роль користувача.");

            if (ModelState.IsValid && await _context.Students
                .AnyAsync(s => s.Email.ToLower() == email.ToLower()))
            {
                ModelState.AddModelError("email", "Користувач із такою електронною адресою вже існує.");
            }

            if (!ModelState.IsValid)
                return View();

            var user = new Student
            {
                Name = name,
                LastName = lastName,
                MiddleName = middleName,
                Email = email,
                Role = role!
            };
            user.Password = _passwordHasher.HashPassword(user, password!);

            _context.Students.Add(user);
            await _context.SaveChangesAsync();

            HttpContext.Session.Clear();
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetInt32("UserId", user.Id);
            return RedirectToAction("Index", "Home");
        }

        // GET залишено для сумісності з наявними посиланнями «Вийти» у Razor.
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Logout")]
        public IActionResult LogoutPost()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var student = await _context.Students.FindAsync(userId.Value);
            return student == null ? NotFound() : View(student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> Profile(
            string name, string? lastName, string? middleName,
            string email, IFormFile? avatarFile)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var student = await _context.Students.FindAsync(userId.Value);
            if (student == null) return NotFound();

            name = (name ?? string.Empty).Trim();
            lastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim();
            middleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim();
            email = (email ?? string.Empty).Trim();

            if (name.Length is < 1 or > 100 ||
                (lastName?.Length ?? 0) > 100 ||
                (middleName?.Length ?? 0) > 100 ||
                email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
            {
                ViewBag.Error = "Перевірте правильність імені та електронної адреси.";
                return View(student);
            }

            if (await _context.Students.AnyAsync(s =>
                s.Id != student.Id && s.Email.ToLower() == email.ToLower()))
            {
                ViewBag.Error = "Ця електронна адреса вже використовується.";
                return View(student);
            }

            string? newAvatarPath = null;
            string? newAvatarPhysicalPath = null;

            if (avatarFile != null && avatarFile.Length > 0)
            {
                if (avatarFile.Length > MaxAvatarSize)
                {
                    ViewBag.Error = "Максимальний розмір аватара — 5 МБ.";
                    return View(student);
                }

                // Перевіряємо сигнатуру файлу, а не довіряємо імені чи ContentType.
                byte[] signature = new byte[12];
                await using (var input = avatarFile.OpenReadStream())
                {
                    var count = await input.ReadAsync(signature.AsMemory());
                    if (count < 12)
                    {
                        ViewBag.Error = "Файл зображення пошкоджений або не підтримується.";
                        return View(student);
                    }
                }

                string? extension = DetectImageExtension(signature);
                if (extension == null)
                {
                    ViewBag.Error = "Дозволено лише зображення JPG, PNG або WEBP.";
                    return View(student);
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"avatar_{Guid.NewGuid():N}{extension}";
                newAvatarPhysicalPath = Path.Combine(uploadsFolder, fileName);
                newAvatarPath = "/uploads/" + fileName;

                await using (var output = new FileStream(
                    newAvatarPhysicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await avatarFile.CopyToAsync(output);
                }
            }

            student.Name = name;
            student.LastName = lastName;
            student.MiddleName = middleName;
            student.Email = email;
            if (newAvatarPath != null) student.AvatarPath = newAvatarPath;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Не залишаємо новий файл без запису в базі даних.
                if (newAvatarPhysicalPath != null && System.IO.File.Exists(newAvatarPhysicalPath))
                    System.IO.File.Delete(newAvatarPhysicalPath);
                throw;
            }

            ViewBag.UserFullName = string.IsNullOrWhiteSpace(student.LastName)
                ? student.Name : $"{student.Name} {student.LastName}";
            ViewBag.UserAvatarPath = student.AvatarPath;
            ViewBag.Message = "Профіль успішно оновлено!";
            return View(student);
        }

        private static string? DetectImageExtension(byte[] bytes)
        {
            // PNG
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E &&
                bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A &&
                bytes[6] == 0x1A && bytes[7] == 0x0A)
                return ".png";

            // JPEG
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return ".jpg";

            // WEBP: RIFF....WEBP
            if (bytes[0] == (byte)'R' && bytes[1] == (byte)'I' &&
                bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
                bytes[8] == (byte)'W' && bytes[9] == (byte)'E' &&
                bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
                return ".webp";

            return null;
        }
    }
}
