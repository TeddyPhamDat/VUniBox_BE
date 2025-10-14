using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VUniBox.Services.Chatbot;
using VUniBox.Services.Quota;
using VUniBox.Models.DTO.Response;
using System.Linq;

namespace VUniBox.Controllers
{
    [ApiController]
    [Route("api/chatbot")]
    /// <summary>
    /// Controller for handling chatbot interactions.
    /// </summary>
    public class ChatbotController : ControllerBase
    {
        private readonly IGeminiChatbotService _chatbotService;
        private readonly SessionChatService _sessionChatService;
        private readonly IQuotaManagementService _quotaManagementService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatbotController"/> class.
        /// </summary>
        /// <param name="chatbotService">The Gemini chatbot service.</param>
        /// <param name="sessionChatService">The session chat service.</param>
        /// <param name="quotaManagementService">The quota management service.</param>
        public ChatbotController(
            IGeminiChatbotService chatbotService, 
            SessionChatService sessionChatService,
            IQuotaManagementService quotaManagementService)
        {
            _chatbotService = chatbotService;
            _sessionChatService = sessionChatService;
            _quotaManagementService = quotaManagementService;
        }

        /// <summary>
        /// Sends a message to the chatbot and receives a response.
        /// Note: Citation generation is restricted - use the dedicated Citation feature instead.
        /// </summary>
        /// <param name="request">The chat message request containing the user's message.</param>
        /// <returns>An <see cref="IActionResult"/> with the chatbot's response and message count.</returns>
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Yêu cầu không hợp lệ", 400));
                }

                if (request.UserId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID người dùng không hợp lệ", 400));
                }

                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(ApiResponse<object>.Fail("Tin nhắn không được để trống", 400));
                }

                // Check chatbot quota before processing
                var canUseChatbot = await _quotaManagementService.CanUseChatbotAsync(request.UserId);
                if (!canUseChatbot)
                {
                    return BadRequest(ApiResponse<object>.Fail(
                        "Bạn đã vượt quá giới hạn sử dụng chatbot của gói hiện tại. Vui lòng nâng cấp gói để tiếp tục sử dụng.", 429));
                }

                // Lấy lịch sử chat từ session (tự động liên tục)
                var chatHistory = _sessionChatService.GetChatHistory();

                // Gửi tin nhắn với lịch sử đầy đủ
                var response = await _chatbotService.SendMessageAsync(request.Message, chatHistory);

                // Lưu tin nhắn user vào lịch sử
                _sessionChatService.AddMessageToHistory("user", request.Message);
                
                // Lưu response AI vào lịch sử
                _sessionChatService.AddMessageToHistory("model", response);

                // Increment chatbot usage after successful interaction
                await _quotaManagementService.IncrementChatbotUsageAsync(request.UserId);

                var result = new 
                { 
                    response = response,
                    messageCount = _sessionChatService.GetMessageCount()
                };

                return Ok(ApiResponse<object>.Success(result, "Tin nhắn đã được gửi thành công"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Clears the chat history for the current session.
        /// </summary>
        /// <returns>An <see cref="IActionResult"/> indicating the chat history has been cleared.</returns>
        [HttpPost("clear")]
        public IActionResult ClearHistory()
        {
            _sessionChatService.ClearChatHistory();
            return Ok(new { message = "Chat history cleared." });
        }

        /// <summary>
        /// Retrieves the chat history for the current session.
        /// </summary>
        /// <returns>An <see cref="IActionResult"/> with the chat history and message count.</returns>
        [HttpGet("history")]
        public IActionResult GetHistory()
        {
            var history = _sessionChatService.GetChatHistory();
            var simpleHistory = history.Select(h => new
            {
                role = h.Role,
                message = h.Parts?.FirstOrDefault()?.Text ?? ""
            }).ToList();

            return Ok(new { history = simpleHistory, count = history.Count });
        }
    }

    /// <summary>
    /// Represents a request to send a message to the chatbot.
    /// </summary>
    public class ChatMessageRequest
    {
        /// <summary>
        /// Gets or sets the user ID making the request.
        /// </summary>
        public int UserId { get; set; }
        
        /// <summary>
        /// Gets or sets the message text.
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// Represents a simplified chat turn with a role and text.
    /// </summary>
    public class SimpleChatTurn
    {
        /// <summary>
        /// Gets or sets the role of the chat participant (e.g., "user" or "model").
        /// </summary>
        public string Role { get; set; }
        /// <summary>
        /// Gets or sets the text of the message.
        /// </summary>
        public string Text { get; set; }
    }
}
