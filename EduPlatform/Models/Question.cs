
namespace EduPlatform.Models
{
    public class Question
    {
        public int Id { get; set; }

        // Текст питання
        public string Text { get; set; } = string.Empty;

        // Правильна відповідь (поле сумісності)
        public string CorrectAnswer { get; set; } = string.Empty;

        // Зв'язок із навчальним модулем
        public int ModuleId { get; set; }
        public CourseModule? Module { get; set; }

        // Варіанти відповідей на питання
        public ICollection<Answer> Answers { get; set; }
            = new List<Answer>();
    }
}
