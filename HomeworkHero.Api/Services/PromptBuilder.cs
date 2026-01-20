using System;
using HomeworkHero.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeworkHero.Api.Services;

public interface IPromptBuilder
{
    string BuildStudentPrompt(string studentName, string studentConditions);
}

public sealed class PromptBuilder : IPromptBuilder
{
    private const string StudentNameToken = "{{StudentName}}";
    private const string StudentConditionsToken = "{{StudentConditions}}";
    private const string StudentBasePromptName = "StudentBasePrompt";
    private readonly HomeworkHeroContext _db;

    public PromptBuilder(HomeworkHeroContext db)
    {
        _db = db;
    }

    public string BuildStudentPrompt(string studentName, string studentConditions)
    {
        var basePrompt = _db.Paramiters
            .AsNoTracking()
            .Where(p => p.Name == StudentBasePromptName)
            .Select(p => p.Value)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(basePrompt))
        {
            throw new InvalidOperationException("Student base prompt is not configured.");
        }

        var name = studentName ?? string.Empty;
        var conditions = studentConditions ?? string.Empty;

        return basePrompt
            .Replace(StudentNameToken, name, StringComparison.OrdinalIgnoreCase)
            .Replace(StudentConditionsToken, conditions, StringComparison.OrdinalIgnoreCase);
    }
}
