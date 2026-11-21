
namespace EduPlatform.Models
{
    public class CourseModule
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public int CourseId { get; set; }

        public Course? Course { get; set; }

        public int MaxAttempts { get; set; } = 3;
        public bool IsArchived { get; set; } = false;

        public ICollection<Question> Questions { get; set; }
            = new List<Question>();
    }
}
