using System.ComponentModel.DataAnnotations;

namespace EduPlatform.Models
{
    public class Course
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Вкажіть назву курсу.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "Назва курсу має містити від 3 до 150 символів.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Вкажіть опис курсу.")]
        [StringLength(3000, MinimumLength = 10, ErrorMessage = "Опис курсу має містити від 10 до 3000 символів.")]
        public string Description { get; set; } = string.Empty;

        public bool ShowCorrectAnswers { get; set; } = false;
        public bool IsPublic { get; set; } = true;

        public int? AuthorId { get; set; }
        public Student? Author { get; set; }
        public string? ImagePath { get; set; }

        public ICollection<CourseModule> Modules { get; set; } = new List<CourseModule>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<CourseMaterial> Materials { get; set; } = new List<CourseMaterial>();

        [Required(ErrorMessage = "Оберіть рівень складності.")]
        [RegularExpression("^(Beginner|Intermediate|Advanced)$", ErrorMessage = "Оберіть допустимий рівень складності.")]
        public string Level { get; set; } = "Beginner";

        [Range(1, 10000, ErrorMessage = "Тривалість має бути від 1 до 10000 годин.")]
        public int DurationHours { get; set; } = 10;
    }
}
