namespace EduPlatform.Models
{
    public class CourseMaterial
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = "Текст";

        // Поле для короткого опису (необов'язкове)
        public string? Description { get; set; }

        // Поле для тексту або посилання (необов'язкове) — ОСЬ ЙОГО НЕ ВИСТАЧАЄ
        public string? Content { get; set; }

        public string? FilePath { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int CourseId { get; set; }
        public Course? Course { get; set; }
    }
}