namespace HomeworkHero.Shared.Models;

public record ClassroomSummaryDto(string GroupId, int TeacherId, string TeacherName, List<StudentSummaryDto> Students);

public class CreateClassroomRequest
{
    public int TeacherId { get; set; }

    public string GroupId { get; set; } = string.Empty;
}

public class ClassroomAssignmentRequest
{
    public int TeacherId { get; set; }

    public string GroupId { get; set; } = string.Empty;

    public List<int> StudentIds { get; set; } = new();
}
