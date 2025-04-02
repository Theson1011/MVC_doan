using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace YourNamespace.Controllers
{
    public class ChatController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string openAiApiKey = "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"; // Replace with your actual OpenAI API key

        public ChatController()
        {
            _httpClient = new HttpClient();
        }

        [HttpPost]
        public async Task<IActionResult> GetChatResponse([FromBody] ChatRequest request)
        {
            var payload = new
            {
                model = "gpt-4", // or gpt-3.5-turbo
                messages = new[] { new { role = "user", content = request.Message } }
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiApiKey}");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            return Json(responseBody);
        }
    }


    public class ChatRequest
    {
        public string Message { get; set; }
    }
}
