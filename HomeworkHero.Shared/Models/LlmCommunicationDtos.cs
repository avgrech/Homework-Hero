using System.Text.Json.Serialization;

namespace HomeworkHero.Shared.Models;

public class LlmApiRequest
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "openai";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "gpt-4o-mini";

    [JsonPropertyName("isChat")]
    public bool IsChat { get; set; } = true;

    [JsonPropertyName("chatHistory")]
    public List<LlmChatHistory> ChatHistory { get; set; } = new();
}

public class LlmChatHistory
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class LlmApiResponse
{
    [JsonPropertyName("Success")]
    public bool Success { get; set; }

    [JsonPropertyName("AssistantResponse")]
    public string AssistantResponse { get; set; } = string.Empty;
}
