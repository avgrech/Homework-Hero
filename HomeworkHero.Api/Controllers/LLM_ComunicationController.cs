using System.Net.Http.Json;
using HomeworkHero.Api.Data;
using HomeworkHero.Api.Services;
using HomeworkHero.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace HomeworkHero.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LLM_ComunicationController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly HomeworkHeroContext _db;
    private readonly string? _llmApiUrl;
    private readonly string? _llmApiKey;
    private readonly IPromptBuilder _promptBuilder;

    public LLM_ComunicationController(HttpClient httpClient, HomeworkHeroContext db, IConfiguration configuration, IPromptBuilder promptBuilder)
    {
        _httpClient = httpClient;
        _db = db;
        _llmApiUrl = configuration["LLMApi:Url"];
        _llmApiKey = configuration["LLMApi:ApiKey"];
        _promptBuilder = promptBuilder;
    }

    [HttpPost]
    public async Task<ActionResult<LlmApiResponse>> SendAIRequest([FromBody] LlmChatRequest request)
    {
        var student = await _db.Students
            .AsNoTracking()
            .Include(s => s.Conditions)
            .ThenInclude(sc => sc.Condition)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId);
        var homeworkExists = await _db.HomeworkItems.AnyAsync(h => h.Id == request.HomeworkItemId);

        if (student is null || !homeworkExists)
        {
            return NotFound("Student or homework item not found.");
        }

        var prompt = new StudentPrompt
        {
            StudentId = request.StudentId,
            HomeworkItemId = request.HomeworkItemId,
            SessionId = TrimToLength(request.SessionId, 100),
            PromptText = TrimToLength(request.PromptText, 2000)
        };

        _db.StudentPrompts.Add(prompt);
        await _db.SaveChangesAsync();

        if (string.IsNullOrWhiteSpace(_llmApiUrl))
        {
            return await RespondWithPromptAsync(prompt, StatusCodes.Status500InternalServerError, "LLM API URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_llmApiKey))
        {
            return await RespondWithPromptAsync(prompt, StatusCodes.Status500InternalServerError, "LLM API key is not configured.");
        }

        if (request.Request is null)
        {
            return await RespondWithPromptAsync(prompt, StatusCodes.Status400BadRequest, "LLM request payload is missing.");
        }

        string systemPrompt;
        try
        {
            systemPrompt = _promptBuilder.BuildStudentPrompt($"{student.FirstName} {student.LastName}", BuildStudentConditions(student));
        }
        catch (InvalidOperationException ex)
        {
            return await RespondWithPromptAsync(prompt, StatusCodes.Status500InternalServerError, ex.Message);
        }

        request.Request.ApiKey = _llmApiKey;
        request.Request.ChatHistory = await BuildChatHistoryAsync(request, systemPrompt);

         using var response = await _httpClient.PostAsJsonAsync(_llmApiUrl, request.Request);
        if (!response.IsSuccessStatusCode)
        {
            return await RespondWithPromptAsync(prompt, (int)response.StatusCode, "LLM API call failed.");
        }

        var result = await response.Content.ReadFromJsonAsync<LlmApiResponse>();
        if (result is null)
        {
            return await RespondWithPromptAsync(prompt, StatusCodes.Status502BadGateway, "Invalid response from LLM API.");
        }

        prompt.ResponseText = TrimToLength(result.AssistantResponse, 4000);
        await _db.SaveChangesAsync();

        return Ok(result);
    }

    private async Task<List<LlmChatHistory>> BuildChatHistoryAsync(LlmChatRequest request, string systemPrompt)
    {
        var history = new List<LlmChatHistory>
        {
            new()
            {
                Role = "system",
                Content = systemPrompt
            }
        };

        var sessionPrompts = await _db.StudentPrompts
            .AsNoTracking()
            .Where(p => p.StudentId == request.StudentId
                        && p.HomeworkItemId == request.HomeworkItemId
                        && p.SessionId == request.SessionId)
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .ToListAsync();

        foreach (var sessionPrompt in sessionPrompts)
        {
            history.Add(new LlmChatHistory
            {
                Role = "user",
                Content = sessionPrompt.PromptText
            });

            if (!string.IsNullOrWhiteSpace(sessionPrompt.ResponseText))
            {
                history.Add(new LlmChatHistory
                {
                    Role = "assistant",
                    Content = sessionPrompt.ResponseText
                });
            }
        }

        return history;
    }

    private static string BuildStudentConditions(Student student)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(student.Details))
        {
            parts.Add(student.Details.Trim());
        }

        foreach (var studentCondition in student.Conditions)
        {
            var conditionName = studentCondition.Condition?.Name;
            if (string.IsNullOrWhiteSpace(conditionName))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(studentCondition.Comments))
            {
                parts.Add($"{conditionName}: {studentCondition.Comments.Trim()}");
            }
            else
            {
                parts.Add(conditionName);
            }
        }

        return string.Join("; ", parts);
    }

    private async Task<ActionResult<LlmApiResponse>> RespondWithPromptAsync(StudentPrompt prompt, int statusCode, string message)
    {
        prompt.ResponseText = TrimToLength(message, 4000);
        await _db.SaveChangesAsync();
        return StatusCode(statusCode, message);
    }

    private static string TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
