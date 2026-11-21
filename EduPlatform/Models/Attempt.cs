namespace EduPlatform.Models
{
    public class Attempt
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public Student? Student { get; set; }
        public int ModuleId { get; set; }
        public CourseModule? Module { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public int Score { get; set; }
        public bool IsPractice { get; set; } = false;
        public string? QuestionOrderJson { get; set; }
        public ICollection<AttemptAnswer> AttemptAnswers { get; set; } = new List<AttemptAnswer>();
    }
}
