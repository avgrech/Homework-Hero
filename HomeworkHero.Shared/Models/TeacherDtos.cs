namespace HomeworkHero.Shared.Models;

public class StudentSummaryDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsChatBlocked { get; set; }
}

public record GroupSummaryDto(string GroupId, List<StudentSummaryDto> Students);

public record StudentHomeworkSummaryDto(
    int HomeworkId,
    string Title,
    string Subject,
    DateOnly DueDate,
    string Status,
    DateTime? SubmittedAt);

public record StudentHomeworkDetailDto(
    int HomeworkId,
    string Title,
    string Subject,
    DateOnly DueDate,
    string Status,
    string? QuestionText,
    string? StudentAnswer,
    DateTime? SubmittedAt,
    List<StudentPromptDto> Prompts);

public record StudentPromptDto(int Id, string PromptText, string? ResponseText, DateTime CreatedAt);

public record NotificationDto(int Id, string Message, DateTime CreatedAt, bool IsRead, int? StudentId, int? HomeworkItemId);

public record FlagStudentRequest(bool IsChatBlocked, string? Notes);

public record HomeworkResultStudentDto(
    int HomeworkResultId,
    int StudentId,
    string StudentName,
    DateTime SubmittedAt,
    string? ResultText,
    string? ResultImageUrl);

public record StudentCorrectionRequest(decimal? Mark, string? Notes, int TeacherId);

public record StudentCorrectionDto(
    int Id,
    int HomeworkResultId,
    int TeacherId,
    decimal? Mark,
    string? Notes,
    DateTime CreatedAt);
