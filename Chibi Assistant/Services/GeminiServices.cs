using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChibiAssistant.Services
{
    /// <summary>
    /// Calls Google Gemini API for AI chat.
    /// NuGet: System.Net.Http.Json (included in .NET 6+)
    /// Set your API key in App.xaml.cs or pass via constructor.
    /// Get your key at: https://aistudio.google.com/app/apikey
    /// </summary>
    public class GeminiService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly List<object> _history = new();

        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

        // Chibi's personality system prompt
        private const string SystemPrompt =
            "You are Chibi, a cute and cheerful AI assistant with a playful personality. " +
            "You speak in a friendly, warm tone. You can respond in Indonesian or English " +
            "depending on what the user uses. Keep responses concise and conversational. " +
            "Occasionally use cute expressions like 'Nya~', 'Uwu', or '(✿◠‿◠)' sparingly. " +
            "You love helping and always try your best!";

        public GeminiService(string apiKey)
        {
            _apiKey = apiKey;
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<string> SendMessageAsync(string userMessage)
        {
            // Add user message to history
            _history.Add(new
            {
                role = "user",
                parts = new[] { new { text = userMessage } }
            });

            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = SystemPrompt } }
                },
                contents = _history,
                generationConfig = new
                {
                    temperature = 0.9,
                    maxOutputTokens = 512
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{BaseUrl}?key={_apiKey}";
            var response = await _http.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API error {response.StatusCode}: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            string replyText = result
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "...";

            // Add assistant reply to history (for multi-turn context)
            _history.Add(new
            {
                role = "model",
                parts = new[] { new { text = replyText } }
            });

            // Keep history manageable (last 20 turns)
            if (_history.Count > 40)
                _history.RemoveRange(0, 2);

            return replyText;
        }

        public void ClearHistory()
        {
            _history.Clear();
        }
    }
}