using VUniBox.Models.DTO.Response;
using VUniBox.Models.Enum;

namespace VUniBox.Services.Classification
{
    public class UrlClassificationService : IUrlClassificationService
    {
        private readonly Dictionary<string, DocumentType> _urlKeywordMapping;

        public UrlClassificationService()
        {
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
                
                // News sites
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
                { "times", DocumentType.Newspaper }
            };
        }

        public async Task<ClassificationResponse> ClassifyUrlAsync(string url)
        {
            return await Task.FromResult(ClassifyUrl(url));
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

