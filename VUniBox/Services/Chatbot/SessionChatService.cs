using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Text.Json;
using VUniBox.Services.Chatbot;

namespace VUniBox.Services.Chatbot
{
    public class SessionChatService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string CHAT_HISTORY_KEY = "ChatHistory";

        public SessionChatService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Lấy lịch sử chat từ session
        public List<GeminiChatbotService.ChatTurn> GetChatHistory()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var historyJson = session?.GetString(CHAT_HISTORY_KEY);
            
            if (string.IsNullOrEmpty(historyJson))
            {
                return new List<GeminiChatbotService.ChatTurn>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<GeminiChatbotService.ChatTurn>>(historyJson) 
                       ?? new List<GeminiChatbotService.ChatTurn>();
            }
            catch
            {
                return new List<GeminiChatbotService.ChatTurn>();
            }
        }

        // Thêm message mới vào lịch sử
        public void AddMessageToHistory(string role, string message)
        {
            var history = GetChatHistory();
            
            history.Add(new GeminiChatbotService.ChatTurn
            {
                Role = role,
                Parts = new[] { new GeminiChatbotService.ContentPart { Text = message } }
            });

            // Giới hạn lịch sử chỉ 20 tin nhắn gần nhất để tránh session quá lớn
            if (history.Count > 20)
            {
                history.RemoveRange(0, history.Count - 20);
            }

            SaveChatHistory(history);
        }

        // Lưu toàn bộ lịch sử vào session
        public void SaveChatHistory(List<GeminiChatbotService.ChatTurn> history)
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var historyJson = JsonSerializer.Serialize(history);
            session?.SetString(CHAT_HISTORY_KEY, historyJson);
        }

        // Xóa lịch sử chat (reset conversation)
        public void ClearChatHistory()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            session?.Remove(CHAT_HISTORY_KEY);
        }

        // Lấy số lượng tin nhắn trong lịch sử
        public int GetMessageCount()
        {
            return GetChatHistory().Count;
        }
    }
}