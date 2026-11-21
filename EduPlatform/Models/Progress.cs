namespace EduPlatform.Models
{
    public class Progress
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public int CompletedTests { get; set; }
        public int BestScore { get; set; }
        public DateTime LastAttempt { get; set; }
    }
}