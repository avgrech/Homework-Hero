namespace HomeworkHero.Shared.Models;

public record ClassroomSummaryDto(string GroupId, int? TeacherId, string TeacherName, List<StudentSummaryDto> Students);

public class CreateClassroomRequest
{
    public string GroupId { get; set; } = string.Empty;

    public int? TeacherId { get; set; }
}

public class ClassroomAssignmentRequest
{
    public int TeacherId { get; set; }

    public string GroupId { get; set; } = string.Empty;

    public List<int> StudentIds { get; set; } = new();
}
