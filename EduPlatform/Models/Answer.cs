
namespace EduPlatform.Models
{
    public class Answer
    {
        public int Id { get; set; }

        // Текст варіанта відповіді
        public string Text { get; set; } = string.Empty;

        // Ознака правильної відповіді
        public bool IsCorrect { get; set; }

        // Зв'язок із питанням
        public int QuestionId { get; set; }
        public Question? Question { get; set; }
    }
}
