namespace EduPlatform.Models
{
    public class LibraryItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "Стаття"; 

        public string? Url { get; set; }

        public string? FilePath { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Хто додав (викладач)
        public int? AuthorId { get; set; }
        public Student? Author { get; set; }
    }
}