
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Data;
using EduPlatform.Models;
using System.IO.Compression;

namespace EduPlatform.Controllers
{
    public class LibraryController : BaseController
    {
        private const long MaxFileSize = 5 * 1024 * 1024;

        private static readonly string[] AllowedExtensions =
        {
            ".pdf",
            ".docx"
        };

        public LibraryController(ApplicationDbContext context)
            : base(context)
        {
        }

        // Перегляд бібліотеки для всіх користувачів.
        public async Task<IActionResult> Index(
            string? searchString,
            string? category)
        {
            var itemsQuery = _context.LibraryItems
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                itemsQuery = itemsQuery.Where(i =>
                    i.Title.Contains(searchString) ||
                    i.Description.Contains(searchString));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                itemsQuery = itemsQuery.Where(i =>
                    i.Category == category);
            }

            var items = await itemsQuery
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            ViewBag.SearchString = searchString;
            ViewBag.Category = category;

            return View(items);
        }

        // Перевірка ролі викладача.
        private bool IsTeacher()
        {
            return HttpContext.Session.GetString("UserRole")
                == "Teacher"
                && HttpContext.Session.GetInt32("UserId").HasValue;
        }

        // GET: Форма створення матеріалу.
        public IActionResult Create()
        {
            if (!IsTeacher())
            {
                return Forbid();
            }

            return View();
        }

        // POST: Створення матеріалу бібліотеки.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string title,
            string description,
            string category,
            string? url,
            IFormFile? file)
        {
            if (!IsTeacher())
            {
                return Forbid();
            }

            title = title?.Trim() ?? string.Empty;
            description = description?.Trim() ?? string.Empty;
            category = category?.Trim() ?? string.Empty;
            url = url?.Trim();

            // Перевірка обов'язкових полів.
            if (string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(description))
            {
                ViewBag.Error =
                    "Заповніть назву та опис матеріалу.";

                return View();
            }

            if (title.Length > 150 ||
                description.Length > 3000)
            {
                ViewBag.Error =
                    "Назва або опис матеріалу занадто довгі.";

                return View();
            }

            // Перевірка категорії.
            var allowedCategories = new[]
            {
                "Книга",
                "Стаття",
                "Відео",
                "Файл"
            };

            if (!allowedCategories.Contains(category))
            {
                ViewBag.Error =
                    "Оберіть допустиму категорію матеріалу.";

                return View();
            }

            // Перевірка посилання.
            if (!string.IsNullOrWhiteSpace(url))
            {
                if (url.Length > 2048 ||
                    !Uri.TryCreate(
                        url,
                        UriKind.Absolute,
                        out var parsedUrl) ||
                    (parsedUrl.Scheme != Uri.UriSchemeHttp &&
                     parsedUrl.Scheme != Uri.UriSchemeHttps))
                {
                    ViewBag.Error =
                        "Вкажіть коректне посилання HTTP або HTTPS.";

                    return View();
                }
            }

            MemoryStream? uploadedData = null;
            string? extension = null;

            // Перевірка завантаженого файлу.
            if (file != null)
            {
                if (file.Length == 0)
                {
                    ViewBag.Error =
                        "Вибраний файл порожній.";

                    return View();
                }

                if (file.Length > MaxFileSize)
                {
                    ViewBag.Error =
                        "Розмір файлу не повинен перевищувати 5 МБ.";

                    return View();
                }

                extension = Path.GetExtension(
                    file.FileName).ToLowerInvariant();

                if (!AllowedExtensions.Contains(extension))
                {
                    ViewBag.Error =
                        "Дозволено завантажувати лише PDF та DOCX.";

                    return View();
                }

                uploadedData = new MemoryStream();

                try
                {
                    await file.CopyToAsync(uploadedData);

                    // Додаткова перевірка фактичного розміру.
                    if (uploadedData.Length == 0 ||
                        uploadedData.Length > MaxFileSize)
                    {
                        ViewBag.Error =
                            "Файл порожній або перевищує 5 МБ.";

                        return View();
                    }

                    uploadedData.Position = 0;

                    if (!IsValidDocument(
                        uploadedData,
                        extension))
                    {
                        ViewBag.Error =
                            "Вміст файлу не відповідає формату PDF або DOCX.";

                        return View();
                    }
                }
                finally
                {
                    // Потік буде звільнено після завершення перевірок
                    // та збереження матеріалу.
                }
            }

            try
            {
                var item = new LibraryItem
                {
                    Title = title,
                    Description = description,
                    Category = category,
                    Url = url,
                    AuthorId = HttpContext.Session.GetInt32("UserId"),
                    CreatedAt = DateTime.UtcNow
                };

                string? savedFilePath = null;

                if (uploadedData != null && extension != null)
                {
                    var uploadsFolder = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "uploads");

                    Directory.CreateDirectory(uploadsFolder);

                    // Назва файлу генерується сервером.
                    var uniqueFileName =
                        $"{Guid.NewGuid():N}{extension}";

                    savedFilePath = Path.Combine(
                        uploadsFolder,
                        uniqueFileName);

                    uploadedData.Position = 0;

                    await using (var stream = new FileStream(
                        savedFilePath,
                        FileMode.CreateNew))
                    {
                        await uploadedData.CopyToAsync(stream);
                    }

                    item.FilePath =
                        "/uploads/" + uniqueFileName;
                }

                try
                {
                    _context.LibraryItems.Add(item);
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Якщо запис у БД не вдався,
                    // прибираємо щойно завантажений файл.
                    if (savedFilePath != null &&
                        System.IO.File.Exists(savedFilePath))
                    {
                        System.IO.File.Delete(savedFilePath);
                    }

                    throw;
                }

                TempData["Success"] =
                    "Матеріал успішно додано до бібліотеки.";

                return RedirectToAction("Index");
            }
            finally
            {
                if (uploadedData != null)
                {
                    await uploadedData.DisposeAsync();
                }
            }
        }

        // Перевірка вмісту PDF та DOCX.
        private static bool IsValidDocument(
            Stream stream,
            string extension)
        {
            try
            {
                stream.Position = 0;

                if (extension == ".pdf")
                {
                    byte[] header = new byte[5];

                    if (stream.Read(header, 0, 5) != 5)
                    {
                        return false;
                    }

                    return header[0] == 0x25 &&
                           header[1] == 0x50 &&
                           header[2] == 0x44 &&
                           header[3] == 0x46 &&
                           header[4] == 0x2D;
                }

                if (extension == ".docx")
                {
                    using var archive = new ZipArchive(
                        stream,
                        ZipArchiveMode.Read,
                        leaveOpen: true);

                    // DOCX є ZIP-архівом із документом Word.
                    return archive.GetEntry(
                        "[Content_Types].xml") != null
                        &&
                        archive.GetEntry(
                            "word/document.xml") != null;
                }

                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            finally
            {
                stream.Position = 0;
            }
        }

        // POST: Видалення лише власного матеріалу.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsTeacher())
            {
                return Forbid();
            }

            var currentUserId =
                HttpContext.Session.GetInt32("UserId");

            var item = await _context.LibraryItems
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            // Забороняємо видаляти чужі публікації.
            if (item.AuthorId != currentUserId)
            {
                return Forbid();
            }

            _context.LibraryItems.Remove(item);
            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Матеріал успішно видалено з бібліотеки.";

            return RedirectToAction(
                "MyMaterials",
                "Teacher");
        }
    }
}
