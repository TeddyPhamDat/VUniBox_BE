using System.Collections.Generic;
using System.Threading.Tasks;

namespace VUniBox.Services.Chatbot
{
    public interface IGeminiChatbotService
    {
        Task<string> SendMessageAsync(string message, List<GeminiChatbotService.ChatTurn> history);
    }
}
