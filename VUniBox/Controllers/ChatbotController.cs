using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VUniBox.Services.Chatbot;
using System.Linq;

namespace VUniBox.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatbotController : ControllerBase
    {
        private readonly IGeminiChatbotService _chatbotService;
        private readonly SessionChatService _sessionChatService;

        public ChatbotController(IGeminiChatbotService chatbotService, SessionChatService sessionChatService)
        {
            _chatbotService = chatbotService;
            _sessionChatService = sessionChatService;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Message cannot be empty." });
            }

            // Lấy lịch sử chat từ session (tự động liên tục)
            var chatHistory = _sessionChatService.GetChatHistory();

            // Gửi tin nhắn với lịch sử đầy đủ
            var response = await _chatbotService.SendMessageAsync(request.Message, chatHistory);

            // Lưu tin nhắn user vào lịch sử
            _sessionChatService.AddMessageToHistory("user", request.Message);
            
            // Lưu response AI vào lịch sử
            _sessionChatService.AddMessageToHistory("model", response);

            return Ok(new { 
                response = response,
                messageCount = _sessionChatService.GetMessageCount()
            });
        }

        [HttpPost("clear")]
        public IActionResult ClearHistory()
        {
            _sessionChatService.ClearChatHistory();
            return Ok(new { message = "Chat history cleared." });
        }

        [HttpGet("history")]
        public IActionResult GetHistory()
        {
            var history = _sessionChatService.GetChatHistory();
            var simpleHistory = history.Select(h => new {
                role = h.Role,
                message = h.Parts?.FirstOrDefault()?.Text ?? ""
            }).ToList();

            return Ok(new { history = simpleHistory, count = history.Count });
        }
    }

    public class ChatMessageRequest
    {
        public string Message { get; set; }
    }

    public class SimpleChatTurn
    {
        public string Role { get; set; }
        public string Text { get; set; }
    }
}
