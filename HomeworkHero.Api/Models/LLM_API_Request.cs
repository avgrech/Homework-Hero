namespace HomeworkHero.Api.Models
{
    public class LLM_API_Request
    {
        public string apiKey { get; set; }
        public string provider { get; set; }
        public string model { get; set; }
        public bool isChat { get; set; }
        public Chathistory[] chatHistory { get; set; }
    }
    public class Chathistory
    {
        public string role { get; set; }
        public string content { get; set; }
    }
}
