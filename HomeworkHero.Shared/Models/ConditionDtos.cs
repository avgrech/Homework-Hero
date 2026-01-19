namespace HomeworkHero.Shared.Models;

public record ConditionSummaryDto(int Id, string Name, string Description, int AssignedStudentCount);

public record ConditionUpdateRequest(string Name, string? Description);

public record ConditionDeleteRequest(int? ReplacementConditionId, bool RemoveAssignments);
