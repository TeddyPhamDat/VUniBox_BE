using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.Enum;
using System.Text.Json.Serialization;

namespace VUniBox.Services.Classification
{
    public class GeminiClassificationService : IGeminiClassificationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiClassificationService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GoogleAI:ApiKey"];
        }

        public async Task<ClassificationResponse> ClassifyUrlWithAIAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return new ClassificationResponse
                {
                    Success = false,
                    DocumentType = DocumentType.Others,
                    TypeName = "Others",
                    Message = "URL is empty or null for AI classification."
                };
            }

            // Prompt engineering for Gemini to classify URL
            var prompt = $"Classify the following URL into one of these categories: Book, Newspaper, Research, or Others. " +
                         $"Provide the classification as a single word (e.g., 'Book', 'Newspaper', 'Research', 'Others').\n" +
                         $"URL: {url}";

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                }
            };

            var requestJson = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}")
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };

            try
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[GeminiClassificationService] Raw JSON Response for {url}:\n{responseJson}");

                GeminiApiResponse geminiResult;
                try
                {
                    geminiResult = JsonSerializer.Deserialize<GeminiApiResponse>(responseJson);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"[GeminiClassificationService] Error deserializing Gemini response: {ex.Message}");
                    return new ClassificationResponse
                    {
                        Success = false,
                        DocumentType = DocumentType.Others,
                        TypeName = "Others",
                        Message = $"Failed to deserialize Gemini response: {ex.Message}"
                    };
                }

                if (geminiResult?.Candidates == null || geminiResult.Candidates.Length == 0)
                {
                    Console.WriteLine("[GeminiClassificationService] Gemini response has no candidates.");
                    return new ClassificationResponse
                    {
                        Success = false,
                        DocumentType = DocumentType.Others,
                        TypeName = "Others",
                        Message = "Gemini response has no candidates."
                    };
                }

                var firstCandidate = geminiResult.Candidates[0];
                if (firstCandidate?.Content?.Parts == null || firstCandidate.Content.Parts.Length == 0)
                {
                    Console.WriteLine("[GeminiClassificationService] First Gemini candidate has no content parts.");
                    return new ClassificationResponse
                    {
                        Success = false,
                        DocumentType = DocumentType.Others,
                        TypeName = "Others",
                        Message = "First Gemini candidate has no content parts."
                    };
                }

                string classificationText = firstCandidate.Content.Parts[0].Text?.Trim();
                Console.WriteLine($"[GeminiClassificationService] Explicitly Trimmed Classification Text: \'{classificationText}'");

                if (string.IsNullOrEmpty(classificationText))
                {
                    Console.WriteLine("[GeminiClassificationService] Trimmed classification text is null or empty.");
                    return new ClassificationResponse
                    {
                        Success = false,
                        DocumentType = DocumentType.Others,
                        TypeName = "Others",
                        Message = "Trimmed classification text is null or empty."
                    };
                }

                DocumentType detectedType = DocumentType.Others;
                string typeName = "Others";

                if (classificationText.Equals("Book", StringComparison.OrdinalIgnoreCase))
                {
                    detectedType = DocumentType.Book;
                    typeName = "Book";
                }
                else if (classificationText.Equals("Newspaper", StringComparison.OrdinalIgnoreCase))
                {
                    detectedType = DocumentType.Newspaper;
                    typeName = "Newspaper";
                }
                else if (classificationText.Equals("Research", StringComparison.OrdinalIgnoreCase))
                {
                    detectedType = DocumentType.Research;
                    typeName = "Research";
                }

                Console.WriteLine($"[GeminiClassificationService] Final Detected Type (after mapping): {detectedType}, TypeName: {typeName}");

                return new ClassificationResponse
                {
                    Success = true,
                    DocumentType = detectedType,
                    TypeName = typeName,
                    Message = $"URL classified by AI as {typeName}"
                };
            }
            catch (HttpRequestException httpEx)
            {
                // Log HTTP request errors (e.g., network issues, invalid API key)
                Console.WriteLine($"HTTP Error calling Gemini API: {httpEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                // Log JSON parsing errors
                Console.WriteLine($"JSON Parsing Error from Gemini API: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                // Log other unexpected errors
                Console.WriteLine($"Error in GeminiClassificationService: {ex.Message}");
            }

            return new ClassificationResponse
            {
                Success = false,
                DocumentType = DocumentType.Others,
                TypeName = "Others",
                Message = "Failed to classify URL using AI, defaulting to Others."
            };
        }

        // Helper classes to parse JSON from Gemini (similar to CitationService)
        private class GeminiApiResponse
        {
            [JsonPropertyName("candidates")]
            public Candidate[] Candidates { get; set; }
        }

        private class Candidate
        {
            [JsonPropertyName("content")]
            public Content Content { get; set; }
        }

        private class Content
        {
            [JsonPropertyName("parts")]
            public Part[] Parts { get; set; }
        }

        private class Part
        {
            [JsonPropertyName("text")]
            public string Text { get; set; }
        }
    }
}
