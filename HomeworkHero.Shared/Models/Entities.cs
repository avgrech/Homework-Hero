using System.ComponentModel.DataAnnotations;

namespace HomeworkHero.Shared.Models;

public class Student
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateOnly DateOfBirth { get; set; }

    [MaxLength(1000)]
    public string Details { get; set; } = string.Empty;

    public bool IsChatBlocked { get; set; }

    public ICollection<StudentCondition> Conditions { get; set; } = new List<StudentCondition>();
    public ICollection<StudentTeacher> Enrollments { get; set; } = new List<StudentTeacher>();
    public ICollection<StudentAction> Actions { get; set; } = new List<StudentAction>();
    public ICollection<StudentPrompt> Prompts { get; set; } = new List<StudentPrompt>();
    public ICollection<HomeworkResult> Results { get; set; } = new List<HomeworkResult>();
}

public class Teacher
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Details { get; set; } = string.Empty;

    public ICollection<StudentTeacher> Students { get; set; } = new List<StudentTeacher>();
    public ICollection<HomeworkItem> Homework { get; set; } = new List<HomeworkItem>();
}

public class Condition
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public ICollection<StudentCondition> StudentConditions { get; set; } = new List<StudentCondition>();
}

public class StudentCondition
{
    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int ConditionId { get; set; }
    public Condition? Condition { get; set; }

    [MaxLength(1000)]
    public string Comments { get; set; } = string.Empty;
}

public class StudentTeacher
{
    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    [MaxLength(50)]
    public string GroupId { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public class HomeworkItem
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? TextContent { get; set; }

    public string? ImageUrl { get; set; }

    [DataType(DataType.Date)]
    public DateOnly DateAssigned { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [DataType(DataType.Date)]
    public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    [MaxLength(50)]
    public string? AssignedGroupId { get; set; }
    public int? AssignedStudentId { get; set; }
    public Student? AssignedStudent { get; set; }

    public ICollection<StudentAction> Actions { get; set; } = new List<StudentAction>();
    public ICollection<StudentPrompt> Prompts { get; set; } = new List<StudentPrompt>();
    public ICollection<HomeworkResult> Results { get; set; } = new List<HomeworkResult>();
}

public class StudentAction
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int HomeworkItemId { get; set; }
    public HomeworkItem? HomeworkItem { get; set; }

    [MaxLength(100)]
    public string ActionType { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StudentPrompt
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int HomeworkItemId { get; set; }
    public HomeworkItem? HomeworkItem { get; set; }

    [MaxLength(100)]
    public string SessionId { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string PromptText { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? ResponseText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class HomeworkResult
{
    public int Id { get; set; }
    public int HomeworkItemId { get; set; }
    public HomeworkItem? HomeworkItem { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }

    [MaxLength(4000)]
    public string? ResultText { get; set; }

    public string? ResultImageUrl { get; set; }

    public decimal? Score { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

public enum UserRole
{
    Student,
    Teacher,
    Admin
}

public class User
{
    public int Id { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Student;

    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
}

public class Permission
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
}

public class UserPermission
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public int PermissionId { get; set; }
    public Permission? Permission { get; set; }
}

public record LoginRequest(string Email, string Password);

public record LoginResponse(bool Success, string? Role, string? Message = null);

public class Notification
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    public int? HomeworkItemId { get; set; }
    public HomeworkItem? HomeworkItem { get; set; }

    [Required, MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
