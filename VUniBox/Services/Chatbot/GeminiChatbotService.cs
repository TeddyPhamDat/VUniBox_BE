using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic; // Added for List

namespace VUniBox.Services.Chatbot
{
    public class GeminiChatbotService : IGeminiChatbotService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiChatbotService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GoogleAI:ApiKey"]; // Using the same API key configuration
        }

        public async Task<string> SendMessageAsync(string message, List<ChatTurn> history)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Please provide a message to chat with.";
            }

            // Check if user is asking for citation generation
            if (IsCitationRequest(message))
            {
                return "Xin lỗi, tôi không thể tạo citation. Vui lòng sử dụng chức năng Citation chuyên dụng trong ứng dụng để tạo trích dẫn học thuật chính xác và đầy đủ.";
            }

            // Prepare the conversation history for the API request
            var contents = new List<object>();

            // Add system instruction to restrict citation generation
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = "QUAN TRỌNG: Bạn là một chatbot hỗ trợ nghiên cứu học thuật. KHÔNG BAO GIỜ tạo trích dẫn (citations) dưới bất kỳ hình thức nào (APA, MLA, Chicago, Harvard, v.v.). Khi được hỏi về citation, hãy từ chối lịch sự và hướng dẫn người dùng sử dụng chức năng Citation chuyên dụng trong ứng dụng. Bạn có thể giúp đỡ về mọi thứ khác như: giải thích nội dung, tóm tắt, phân tích, gợi ý nghiên cứu, v.v." } }
            });

            contents.Add(new
            {
                role = "model",
                parts = new[] { new { text = "Tôi hiểu. Tôi sẽ không tạo citation dưới bất kỳ hình thức nào và sẽ hướng dẫn người dùng sử dụng chức năng Citation chuyên dụng khi cần. Tôi sẵn sàng hỗ trợ bạn về các vấn đề học thuật khác." } }
            });

            // Add existing history in proper format
            if (history != null && history.Count > 0)
            {
                foreach (var turn in history)
                {
                    contents.Add(new
                    {
                        role = turn.Role,
                        parts = new[] { new { text = turn.Parts?.FirstOrDefault()?.Text ?? "" } }
                    });
                }
            }

            // Add the current user message
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = message } }
            });

            var requestBody = new
            {
                contents = contents
            };

            var requestJson = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            Console.WriteLine($"[GeminiChatbotService] Request JSON: {requestJson}");

            var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}")
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };

            try
            {
                var response = await _httpClient.SendAsync(request);
                
                // Always read response content for debugging
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[GeminiChatbotService] Response Status: {response.StatusCode}");
                Console.WriteLine($"[GeminiChatbotService] Response Content: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    // Return detailed error message
                    Console.WriteLine($"[GeminiChatbotService] Gemini API Error - Status: {response.StatusCode}, Body: {responseContent}");
                    return $"Error from Gemini API ({response.StatusCode}): {responseContent}";
                }

                using (JsonDocument doc = JsonDocument.Parse(responseContent))
                {
                    if (doc.RootElement.TryGetProperty("candidates", out JsonElement candidatesElement) &&
                        candidatesElement.EnumerateArray().Any())
                    {
                        var firstCandidate = candidatesElement.EnumerateArray().First();
                        if (firstCandidate.TryGetProperty("content", out JsonElement contentElement) &&
                            contentElement.TryGetProperty("parts", out JsonElement partsElement) &&
                            partsElement.EnumerateArray().Any())
                        {
                            var firstPart = partsElement.EnumerateArray().First();
                            if (firstPart.TryGetProperty("text", out JsonElement textElement))
                            {
                                var rawText = textElement.GetString();
                                
                                // Double-check response doesn't contain citations
                                if (ContainsCitation(rawText))
                                {
                                    return "Xin lỗi, tôi không thể tạo citation. Vui lòng sử dụng chức năng Citation chuyên dụng trong ứng dụng để tạo trích dẫn học thuật chính xác và đầy đủ.";
                                }
                                
                                // Format the response text for better readability
                                var formattedText = FormatChatbotResponse(rawText);
                                return formattedText;
                            }
                        }
                    }
                }
                return "Could not get a response from the AI.";
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[GeminiChatbotService] HTTP Request Error: {ex.Message}");
                return $"Error communicating with the AI service: {ex.Message}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GeminiChatbotService] General Error: {ex.Message}");
                return $"An unexpected error occurred: {ex.Message}";
            }
        }

        /// <summary>
        /// Checks if the user message is requesting citation generation
        /// </summary>
        /// <param name="message">User message to check</param>
        /// <returns>True if it's a citation request</returns>
        private bool IsCitationRequest(string message)
        {
            var lowerMessage = message.ToLower().Trim();
            
            // Direct citation generation requests
            var directCitationRequests = new[]
            {
                "tạo citation", "tạo trích dẫn", "generate citation", "make citation",
                "format citation", "định dạng trích dẫn", "cite this", "cite for me",
                "viết citation", "viết trích dẫn", "create citation", "citation cho",
                "trích dẫn cho", "citation của", "trích dẫn của"
            };
            
            // Citation format requests
            var formatRequests = new[]
            {
                "apa format", "mla format", "chicago format", "harvard format",
                "ieee format", "vancouver format", "định dạng apa", "định dạng mla",
                "theo apa", "theo mla", "theo chicago", "theo harvard"
            };
            
            // Check for direct citation requests first
            if (directCitationRequests.Any(request => lowerMessage.Contains(request)))
            {
                return true;
            }
            
            // Check for format-specific requests
            if (formatRequests.Any(format => lowerMessage.Contains(format)))
            {
                return true;
            }
            
            // Check for citation-related verbs combined with citation terms
            var actionWords = new[] { "tạo", "viết", "làm", "generate", "create", "make", "format", "write" };
            var citationTerms = new[] { "citation", "trích dẫn", "reference", "tham khảo" };
            
            bool hasAction = actionWords.Any(action => lowerMessage.Contains(action));
            bool hasCitationTerm = citationTerms.Any(term => lowerMessage.Contains(term));
            
            // Only return true if both action and citation term are present
            if (hasAction && hasCitationTerm)
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Checks if the response contains citation formats
        /// </summary>
        /// <param name="response">Response text to check</param>
        /// <returns>True if response contains citations</returns>
        private bool ContainsCitation(string response)
        {
            if (string.IsNullOrEmpty(response))
                return false;
                
            var lowerResponse = response.ToLower();
            
            // More specific citation format patterns to avoid false positives
            var citationPatterns = new[]
            {
                // Very specific APA patterns
                @"\([A-Za-z]+.*?,\s*\d{4}\)", // (Author, 2023) 
                @"retrieved from https?://", // Retrieved from http://
                @"doi:\s*10\.", // DOI: 10.
                @"https://doi\.org/10\.", // https://doi.org/10.
                
                // Very specific MLA patterns  
                @"[A-Za-z]+.*?\.\s*\d{4}\.\s*web\.", // Author. 2023. Web.
                @"accessed\s+\d{1,2}\s+[A-Za-z]+\s+\d{4}", // Accessed 15 Mar 2023
                @"print\.\s*$", // Print. at end of line
                
                // Very specific citation indicators
                @"\bet\s+al\.\s*\(", // et al. (
                @"\bibid\.\s*[,\.]", // ibid.,
                @"\bop\.\s*cit\.\s*[,\.]", // op. cit.,
                @"\bloc\.\s*cit\.\s*[,\.]", // loc. cit.,
                
                // Specific page references in citation context
                @"\bpp\.\s*\d+-\d+", // pp. 123-456
                @"\bp\.\s*\d+[,\.]", // p. 123,
                @"\bvol\.\s*\d+", // vol. 12
                @"\bno\.\s*\d+", // no. 5
                
                // Vietnamese citation indicators in specific formats
                @"tr\.\s*\d+-\d+", // tr. 123-456 (trang)
                @"trang\s*\d+-\d+", // trang 123-456
                @"xuất bản.*?\d{4}", // xuất bản năm 2023
                @"nhà xuất bản.*?:", // nhà xuất bản ABC:
            };
            
            // Check for citation patterns
            foreach (var pattern in citationPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerResponse, pattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            
            // Additional check: multiple citation indicators together
            var citationIndicators = new[] { "et al.", "ibid.", "op. cit.", "loc. cit.", "doi:", "retrieved from" };
            int indicatorCount = citationIndicators.Count(indicator => lowerResponse.Contains(indicator));
            
            return indicatorCount >= 2; // If 2 or more citation indicators, likely a citation
        }

        // Format chatbot response for better readability
        private string FormatChatbotResponse(string rawText)
        {
            if (string.IsNullOrEmpty(rawText))
                return rawText;

            // Step 1: Remove markdown formatting
            var formatted = rawText
                .Replace("**", "")
                .Trim();

            // Step 2: Split into lines and process each line
            var lines = formatted.Split('\n', StringSplitOptions.None);
            var processedLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                // Keep empty lines for proper spacing
                if (string.IsNullOrEmpty(line))
                {
                    processedLines.Add("");
                    continue;
                }
                
                // Convert bullet points
                if (line.StartsWith("*   "))
                {
                    line = "• " + line.Substring(4);
                }
                else if (line.StartsWith("* "))
                {
                    line = "• " + line.Substring(2);
                }
                
                processedLines.Add(line);
            }

            // Step 3: Rejoin with cleaned spacing
            formatted = string.Join("\n", processedLines);

            // Step 4: Clean up multiple consecutive empty lines
            while (formatted.Contains("\n\n\n"))
            {
                formatted = formatted.Replace("\n\n\n", "\n\n");
            }

            return formatted.Trim();
        }

        // Helper classes for JSON serialization of chat history
        public class ChatTurn
        {
            public string Role { get; set; }
            public ContentPart[] Parts { get; set; }
        }

        public class ContentPart
        {
            public string Text { get; set; }
        }
    }
}
