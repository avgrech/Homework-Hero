using System.Net.Http.Json;
using HomeworkHero.Api.Data;
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

    public LLM_ComunicationController(HttpClient httpClient, HomeworkHeroContext db, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _db = db;
        _llmApiUrl = configuration["LLMApi:Url"];
    }

    [HttpPost]
    public async Task<ActionResult<LlmApiResponse>> SendAIRequest([FromBody] LlmChatRequest request)
    {
        if (!await _db.Students.AnyAsync(s => s.Id == request.StudentId) ||
            !await _db.HomeworkItems.AnyAsync(h => h.Id == request.HomeworkItemId))
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
