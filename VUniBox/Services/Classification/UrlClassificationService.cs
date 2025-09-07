using VUniBox.Models.DTO.Response;
using VUniBox.Models.Enum;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace VUniBox.Services.Classification
{
    public class UrlClassificationService : IUrlClassificationService
    {
        private readonly Dictionary<string, DocumentType> _urlKeywordMapping;
        private readonly IGeminiClassificationService _geminiClassificationService;

        public UrlClassificationService(IGeminiClassificationService geminiClassificationService)
        {
            _geminiClassificationService = geminiClassificationService;
            _urlKeywordMapping = new Dictionary<string, DocumentType>(StringComparer.OrdinalIgnoreCase)
            {
                // Research/Academic sites
                { "scholar.google", DocumentType.Research },
                { "researchgate", DocumentType.Research },
                { "arxiv.org", DocumentType.Research },
                { "pubmed", DocumentType.Research },
                { "ieee", DocumentType.Research },
                { "acm", DocumentType.Research },
                { "springer", DocumentType.Research },
                { "elsevier", DocumentType.Research },
                { "doi.org", DocumentType.Research },
                { "jstor", DocumentType.Research },
                { "academia.edu", DocumentType.Research },
                { "ssrn", DocumentType.Research },
                
                // Book sites (including academic book publishers)
                { "amazon", DocumentType.Book },
                { "goodreads", DocumentType.Book },
                { "books.google", DocumentType.Book },
                { "barnesandnoble", DocumentType.Book },
                { "bookdepository", DocumentType.Book },
                { "kobo", DocumentType.Book },
                { "scribd", DocumentType.Book },
                { "sciencedirect.com/book", DocumentType.Book }, // ScienceDirect books
                { "link.springer.com/book", DocumentType.Book }, // Springer books
                { "wiley.com/book", DocumentType.Book }, // Wiley books
                { "cambridge.org/core/books", DocumentType.Book }, // Cambridge books
                { "oxfordacademic.com/book", DocumentType.Book }, // Oxford books
                
                // International News sites
                { "bbc", DocumentType.Newspaper },
                { "cnn", DocumentType.Newspaper },
                { "reuters", DocumentType.Newspaper },
                { "ap.org", DocumentType.Newspaper },
                { "nytimes", DocumentType.Newspaper },
                { "washingtonpost", DocumentType.Newspaper },
                { "theguardian", DocumentType.Newspaper },
                { "bloomberg", DocumentType.Newspaper },
                { "wsj", DocumentType.Newspaper },
                { "ft.com", DocumentType.Newspaper },
                { "news", DocumentType.Newspaper },
                { "times", DocumentType.Newspaper },
                
                // Vietnamese News sites
                { "vnexpress", DocumentType.Newspaper },
                { "tuoitre", DocumentType.Newspaper },
                { "thanhnien", DocumentType.Newspaper },
                { "dantri", DocumentType.Newspaper },
                { "vietnamnet", DocumentType.Newspaper },
                { "vtc", DocumentType.Newspaper },
                { "soha", DocumentType.Newspaper },
                { "zing", DocumentType.Newspaper },
                { "24h", DocumentType.Newspaper },
                { "cafef", DocumentType.Newspaper },
                { "cafebiz", DocumentType.Newspaper },
                { "tienphong", DocumentType.Newspaper },
                { "laodong", DocumentType.Newspaper },
                { "nld", DocumentType.Newspaper },
                { "baomoi", DocumentType.Newspaper },
                { "kenh14", DocumentType.Newspaper },
                { "eva", DocumentType.Newspaper },
                { "vov", DocumentType.Newspaper },
                { "vtv", DocumentType.Newspaper },
                { "vietgiaitri", DocumentType.Newspaper }
            };
        }

        public async Task<ClassificationResponse> ClassifyUrlAsync(string url)
        {
            Console.WriteLine($"[UrlClassificationService] Attempting AI classification for URL: {url}");
            // 1. Try AI-based classification first for specific types
            var aiClassification = await _geminiClassificationService.ClassifyUrlWithAIAsync(url);
            
            Console.WriteLine($"[UrlClassificationService] AI Classification Result - Success: {aiClassification.Success}, Type: {aiClassification.DocumentType}, TypeName: {aiClassification.TypeName}");

            if (aiClassification.Success && 
                (aiClassification.DocumentType == DocumentType.Book || 
                 aiClassification.DocumentType == DocumentType.Newspaper || 
                 aiClassification.DocumentType == DocumentType.Research))
            {
                Console.WriteLine($"[UrlClassificationService] Returning AI classification: {aiClassification.TypeName}");
                return aiClassification;
            }

            // 2. Fallback to rule-based classification if AI didn't provide a specific category or failed
            Console.WriteLine("[UrlClassificationService] AI classification not specific or failed, falling back to rule-based.");
            var ruleBasedClassification = ClassifyUrl(url);
            Console.WriteLine($"[UrlClassificationService] Rule-based Classification Result - Success: {ruleBasedClassification.Success}, Type: {ruleBasedClassification.DocumentType}, TypeName: {ruleBasedClassification.TypeName}");
            return ruleBasedClassification;
        }

        public ClassificationResponse ClassifyUrl(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    return new ClassificationResponse
                    {
                        Success = false,
                        DocumentType = DocumentType.Others,
                        TypeName = "Others",
                        Message = "URL is empty or null"
                    };
                }

                // Validate URL format
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    return new ClassificationResponse
                    {
                        Success = false,
                        DocumentType = DocumentType.Others,
                        TypeName = "Others",
                        Message = "Invalid URL format"
                    };
                }

                var host = uri.Host.ToLower();
                var fullUrl = url.ToLower();

                // Check for exact domain matches first
                foreach (var kvp in _urlKeywordMapping)
                {
                    if (host.Contains(kvp.Key) || fullUrl.Contains(kvp.Key))
                    {
                        return new ClassificationResponse
                        {
                            Success = true,
                            DocumentType = kvp.Value,
                            TypeName = kvp.Value.ToString(),
                            Message = $"URL classified as {kvp.Value} based on keyword '{kvp.Key}'"
                        };
                    }
                }

                // Default to Others if no keywords found
                return new ClassificationResponse
                {
                    Success = true,
                    DocumentType = DocumentType.Others,
                    TypeName = "Others",
                    Message = "No classification keywords found, classified as Others"
                };
            }
            catch (Exception ex)
            {
                return new ClassificationResponse
                {
                    Success = false,
                    DocumentType = DocumentType.Others,
                    TypeName = "Others",
                    Message = $"Error classifying URL: {ex.Message}"
                };
            }
        }
    }
}






