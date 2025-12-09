namespace HomeworkHero.Shared.Models;

public record ClassroomSummaryDto(string GroupId, int TeacherId, string TeacherName, List<StudentSummaryDto> Students);

public record ClassroomAssignmentRequest(int TeacherId, string GroupId, List<int> StudentIds);
