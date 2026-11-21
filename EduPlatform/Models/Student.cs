namespace EduPlatform.Models
{
    public class Student
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; 
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Student";

        // Нові поля
        public string? LastName { get; set; }
        public string? MiddleName { get; set; }
        public string? AvatarPath { get; set; }

        public ICollection<Attempt> Attempts { get; set; } = new List<Attempt>();
        public ICollection<Progress> Progresses { get; set; } = new List<Progress>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}