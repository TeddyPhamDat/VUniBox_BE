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

            // Prepare the conversation history for the API request
            var contents = new List<object>();

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
