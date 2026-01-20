using System.Net.Http.Json;
using HomeworkHero.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace HomeworkHero.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LLM_ComunicationController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly string? _llmApiUrl;

    public LLM_ComunicationController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _llmApiUrl = configuration["LLMApi:Url"];
    }

    [HttpPost]
    public async Task<ActionResult<LLM_API_Response>> SendAIRequest([FromBody] LLM_API_Request request)
    {
        if (string.IsNullOrWhiteSpace(_llmApiUrl))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "LLM API URL is not configured.");
        }

        using var response = await _httpClient.PostAsJsonAsync(_llmApiUrl, request);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, "LLM API call failed.");
        }

        var result = await response.Content.ReadFromJsonAsync<LLM_API_Response>();
        if (result is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Invalid response from LLM API.");
        }

        return Ok(result);
    }
}
