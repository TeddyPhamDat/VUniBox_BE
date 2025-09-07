using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace VUniBox.Services.Citation
{
    public class GeminiCitationService : IGeminiCitationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly bool _useAI = false; // Temporarily disable AI to force fallback

        public GeminiCitationService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GoogleAI:ApiKey"];
        }

        public async Task<(string formatted, string inText)> GenerateCitationAsync(
            string title,
            string authors,
            int? year,
            string publicationDate,
            string type,
            string url,
            string style)
        {
            Console.WriteLine($"[GeminiCitationService] Generating citation for style: {style}");
            Console.WriteLine($"[GeminiCitationService] Input data - Title: {title}, Authors: {authors}, Year: {year}, PublicationDate: {publicationDate}");

            // For now, always use fallback for consistent results
            if (!_useAI)
            {
                Console.WriteLine("[GeminiCitationService] Using direct fallback mode for consistent results");
                var fallbackResult = GenerateAccurateCitation(title, authors, year, url, style, publicationDate);
                Console.WriteLine($"[GeminiCitationService] Fallback result - Formatted: {fallbackResult.formatted}, InText: {fallbackResult.inText}");
                return fallbackResult;
            }

            // AI code (currently disabled)
            try
            {
                var prompt = BuildStyleSpecificPrompt(title, authors, type, url, year, publicationDate, style);
                var citationResult = await CallGeminiAPIAsync(prompt);
                return citationResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GeminiCitationService] AI error: {ex.Message}, falling back to accurate citation");
                return GenerateAccurateCitation(title, authors, year, url, style, publicationDate);
            }
        }

        private (string formatted, string inText) GenerateAccurateCitation(
            string title, string authors, int? year, string url, string style, string publicationDate)
        {
            // Extract year from multiple sources
            var effectiveYear = ExtractYear(year, publicationDate);
            var accessDate = DateTime.Now.ToString("MMMM dd, yyyy");
            
            Console.WriteLine($"[GeminiCitationService] Effective year: {effectiveYear}, Access date: {accessDate}");

            // Clean inputs
            title = title?.Trim() ?? "Untitled";
            authors = authors?.Trim() ?? "Unknown Author";
            url = url?.Trim() ?? "";

            string formatted, inText;

            switch (style.ToUpper())
            {
                case "APA":
                    (formatted, inText) = GenerateAPACitation(authors, effectiveYear, title, url, accessDate);
                    break;

                case "MLA":
                    (formatted, inText) = GenerateMLACitation(authors, title, url, accessDate);
                    break;

                case "CHICAGO":
                    (formatted, inText) = GenerateChicagoCitation(authors, title, url, accessDate, effectiveYear);
                    break;

                case "HARVARD":
                    (formatted, inText) = GenerateHarvardCitation(authors, effectiveYear, title, url, accessDate);
                    break;

                case "IEEE":
                    (formatted, inText) = GenerateIEEECitation(authors, title, url, accessDate);
                    break;

                case "VANCOUVER":
                    (formatted, inText) = GenerateVancouverCitation(authors, title, url, accessDate);
                    break;

                default:
                    (formatted, inText) = GenerateAPACitation(authors, effectiveYear, title, url, accessDate);
                    break;
            }

            return (formatted, inText);
        }

        private int? ExtractYear(int? year, string publicationDate)
        {
            // Priority: explicit year > year from publication date > current year as fallback
            if (year.HasValue && year.Value > 1900 && year.Value <= DateTime.Now.Year + 1)
            {
                return year.Value;
            }

            if (!string.IsNullOrEmpty(publicationDate))
            {
                // Try DateOnly parsing first
                if (DateOnly.TryParse(publicationDate, out var dateOnlyResult))
                {
                    return dateOnlyResult.Year;
                }

                // Try DateTime parsing
                if (DateTime.TryParse(publicationDate, out var dateResult))
                {
                    return dateResult.Year;
                }

                // Try regex extraction for 4-digit years
                var yearMatch = System.Text.RegularExpressions.Regex.Match(publicationDate, @"\b(19|20)\d{2}\b");
                if (yearMatch.Success && int.TryParse(yearMatch.Value, out var regexYear))
                {
                    return regexYear;
                }
            }

            // As a last resort, use current year instead of null to avoid "n.d."
            Console.WriteLine("[GeminiCitationService] No valid year found, using current year as fallback");
            return DateTime.Now.Year;
        }

        private (string formatted, string inText) GenerateAPACitation(string authors, int? year, string title, string url, string accessDate)
        {
            // Ensure we always have a valid year - should not be null after ExtractYear changes
            var yearStr = year?.ToString() ?? DateTime.Now.Year.ToString();
            
            // Format APA citation properly
            var authorPart = FormatAuthorsAPA(authors);
            var urlPart = !string.IsNullOrEmpty(url) ? $"Retrieved {accessDate}, from {url}" : $"Retrieved {accessDate}";
            
            var formatted = $"{authorPart} ({yearStr}). {title}. {urlPart}";
            var inText = $"({GetLastNameAPA(authors)}, {yearStr})";
            
            return (formatted, inText);
        }

        private string FormatAuthorsAPA(string authors)
        {
            if (string.IsNullOrWhiteSpace(authors) || authors == "Unknown Author")
            {
                return "Unknown Author";
            }

            // Handle multiple authors separated by commas, semicolons, or "and"
            var authorList = authors.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(a => a.Trim())
                                   .ToList();

            if (authorList.Count == 1)
            {
                // Handle single author or "and" separated authors
                var singleAuthor = authorList[0];
                if (singleAuthor.Contains(" and "))
                {
                    var parts = singleAuthor.Split(new string[] { " and " }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        return $"{parts[0].Trim()}, & {parts[1].Trim()}";
                    }
                }
                return singleAuthor;
            }
            else if (authorList.Count == 2)
            {
                return $"{authorList[0]}, & {authorList[1]}";
            }
            else
            {
                // More than 2 authors - use first author et al.
                return $"{authorList[0]} et al.";
            }
        }

        private string GetLastNameAPA(string authors)
        {
            if (string.IsNullOrWhiteSpace(authors) || authors == "Unknown Author")
            {
                return "Unknown Author";
            }

            // Get first author's last name for in-text citation
            var firstAuthor = authors.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            
            // Handle "FirstName LastName" format
            var nameParts = firstAuthor.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (nameParts.Length > 1)
            {
                return nameParts.Last(); // Return last name
            }
            
            return firstAuthor; // Return as is if only one part
        }

        private (string formatted, string inText) GenerateMLACitation(string authors, string title, string url, string accessDate)
        {
            var authorPart = FormatAuthorsMLA(authors);
            var urlPart = !string.IsNullOrEmpty(url) ? $" Web. {accessDate}. <{url}>." : $" Web. {accessDate}.";
            var formatted = $"{authorPart} \"{title}.\" {urlPart}";
            var inText = $"({GetLastNameMLA(authors)})";
            return (formatted, inText);
        }

        private (string formatted, string inText) GenerateChicagoCitation(string authors, string title, string url, string accessDate, int? year)
        {
            var authorPart = FormatAuthorsChicago(authors);
            var yearPart = year?.ToString() ?? DateTime.Now.Year.ToString();
            var urlPart = !string.IsNullOrEmpty(url) ? $" Accessed {accessDate}. {url}." : $" Accessed {accessDate}.";
            var formatted = $"{authorPart} \"{title}.\" {yearPart}.{urlPart}";
            var inText = $"({GetLastNameChicago(authors)}, {yearPart})";
            return (formatted, inText);
        }

        private (string formatted, string inText) GenerateHarvardCitation(string authors, int? year, string title, string url, string accessDate)
        {
            var yearStr = year?.ToString() ?? DateTime.Now.Year.ToString();
            var accessDateFormatted = DateTime.Now.ToString("dd MMMM yyyy");
            var authorPart = FormatAuthorsHarvard(authors);
            var urlPart = !string.IsNullOrEmpty(url) ? $" Available at: {url}" : "";
            var formatted = $"{authorPart} ({yearStr}) '{title}'.{urlPart} (Accessed: {accessDateFormatted}).";
            var inText = $"({GetLastNameHarvard(authors)}, {yearStr})";
            return (formatted, inText);
        }

        private (string formatted, string inText) GenerateIEEECitation(string authors, string title, string url, string accessDate)
        {
            var shortAccessDate = DateTime.Now.ToString("MMM. dd, yyyy");
            var authorPart = FormatAuthorsIEEE(authors);
            var urlPart = !string.IsNullOrEmpty(url) ? $" [Online]. Available: {url}." : " [Online].";
            var formatted = $"{authorPart}, \"{title}.\" {urlPart} [Accessed: {shortAccessDate}].";
            var inText = "[1]";
            return (formatted, inText);
        }

        private (string formatted, string inText) GenerateVancouverCitation(string authors, string title, string url, string accessDate)
        {
            var citedDate = DateTime.Now.ToString("yyyy MMM dd");
            var authorPart = FormatAuthorsVancouver(authors);
            var urlPart = !string.IsNullOrEmpty(url) ? $" Available from: {url}" : "";
            var formatted = $"{authorPart} {title} [Internet]. [cited {citedDate}].{urlPart}";
            var inText = "(1)";
            return (formatted, inText);
        }

        // Format methods for different citation styles
        private string FormatAuthorsMLA(string authors) => FormatAuthorsGeneric(authors);
        private string FormatAuthorsChicago(string authors) => FormatAuthorsGeneric(authors);
        private string FormatAuthorsHarvard(string authors) => FormatAuthorsGeneric(authors);
        private string FormatAuthorsIEEE(string authors) => FormatAuthorsGeneric(authors);
        private string FormatAuthorsVancouver(string authors) => FormatAuthorsGeneric(authors);

        private string FormatAuthorsGeneric(string authors)
        {
            if (string.IsNullOrWhiteSpace(authors) || authors == "Unknown Author")
            {
                return "Unknown Author";
            }
            return authors.Trim();
        }

        // Last name extraction methods for different styles
        private string GetLastNameMLA(string authors) => GetLastNameAPA(authors);
        private string GetLastNameChicago(string authors) => GetLastNameAPA(authors);
        private string GetLastNameHarvard(string authors) => GetLastNameAPA(authors);

        // AI methods (currently disabled but kept for future use)
        private string BuildStyleSpecificPrompt(string title, string authors, string type, string url, int? year, string publicationDate, string style)
        {
            return $"Generate {style} citation for: {title} by {authors} ({year}) from {url}";
        }

        private async Task<(string formatted, string inText)> CallGeminiAPIAsync(string prompt)
        {
            // AI implementation (placeholder)
            throw new Exception("AI temporarily disabled for testing");
        }

        // Helper classes for JSON parsing (kept for future AI use)
        private class GeminiCitationApiResponse
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

        private class CitationOutput
        {
            [JsonPropertyName("formattedCitation")]
            public string FormattedCitation { get; set; }

            [JsonPropertyName("inTextCitation")]
            public string InTextCitation { get; set; }
        }
    }
}
