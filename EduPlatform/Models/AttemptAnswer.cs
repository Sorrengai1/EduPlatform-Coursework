namespace EduPlatform.Models
{
    public class AttemptAnswer
    {
        public int Id { get; set; }

        public int AttemptId { get; set; }
        public Attempt? Attempt { get; set; }

        public int QuestionId { get; set; }
        public Question? Question { get; set; }

        public string SelectedAnswer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}