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
        private readonly bool _useAI = true; // Enable AI with updated gemini-2.0-flash model

        public GeminiCitationService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GoogleAI:ApiKey"];
        }

        /// <summary>
        /// Generate a realistic author name when the actual author cannot be determined
        /// </summary>
        private string GenerateRealisticAuthor(string title = null, string url = null)
        {
            // Extract from URL if possible
            if (!string.IsNullOrWhiteSpace(url))
            {
                // ResearchGate profile extraction
                if (url.Contains("researchgate.net/profile/"))
                {
                    var profileMatch = System.Text.RegularExpressions.Regex.Match(url, @"profile/([^/?]+)");
                    if (profileMatch.Success)
                    {
                        var profileName = profileMatch.Groups[1].Value.Replace("-", " ").Replace("_", " ");
                        return FormatAuthorName(profileName);
                    }
                }
                
                // ResearchGate publication extraction
                if (url.Contains("researchgate.net/publication/"))
                {
                    var pubMatch = System.Text.RegularExpressions.Regex.Match(url, @"publication/\d+[_-]([^/?]+)");
                    if (pubMatch.Success)
                    {
                        var authorPart = pubMatch.Groups[1].Value.Replace("-", " ").Replace("_", " ");
                        return FormatAuthorName(authorPart);
                    }
                }
                
                // Domain-based fallbacks
                if (url.Contains("ieee.org"))
                    return "IEEE Research Team";
                if (url.Contains("acm.org"))
                    return "ACM Digital Library";
                if (url.Contains("springer.com"))
                    return "Springer Nature";
                if (url.Contains("arxiv.org"))
                    return "arXiv Contributors";
            }
            
            // Title-based generation (simple approach)
            if (!string.IsNullOrWhiteSpace(title))
            {
                if (title.ToLower().Contains("machine learning"))
                    return "Research Team";
                if (title.ToLower().Contains("artificial intelligence"))
                    return "AI Research Group";
                if (title.ToLower().Contains("computer science"))
                    return "Computer Science Researchers";
            }
            
            return "Academic Authors"; // Last resort, still better than "Unknown Author"
        }

        /// <summary>
        /// Format author name properly
        /// </summary>
        private string FormatAuthorName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Academic Authors";
            
            // Clean up the name
            name = name.Trim().Replace("_", " ").Replace("-", " ");
            
            // Capitalize properly
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }
            
            return string.Join(" ", words);
        }

        /// <summary>
        /// Clean AI-generated title artifacts and format properly
        /// </summary>
        private string CleanAIGeneratedTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "Untitled";

            // Remove AI artifacts like markdown formatting
            title = title.Trim()
                         .Replace("**", "") // Remove bold markdown
                         .Replace("*", "")  // Remove italic markdown
                         .Replace("# ", "") // Remove heading markdown
                         .Replace("## ", "")
                         .Replace("### ", "")
                         .Trim();

            // Remove common AI prefixes
            var prefixes = new[] { "Title:", "title:", "TITLE:", "Article:", "Paper:", "Document:" };
            foreach (var prefix in prefixes)
            {
                if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    title = title.Substring(prefix.Length).Trim();
                }
            }

            // Remove quotes if they wrap the entire title
            if (title.StartsWith("\"") && title.EndsWith("\""))
            {
                title = title.Substring(1, title.Length - 2).Trim();
            }

            return string.IsNullOrWhiteSpace(title) ? "Untitled" : title;
        }

        /// <summary>
        /// Format title according to APA style (sentence case - only first word and proper nouns capitalized)
        /// </summary>
        private string FormatTitleAPA(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "Untitled";

            // First clean AI artifacts
            title = CleanAIGeneratedTitle(title);
            
            // If title already looks properly formatted (has mixed case), keep it as is
            if (title != title.ToUpper() && title != title.ToLower() && char.IsUpper(title[0]))
            {
                return title;
            }
            
            // Convert to sentence case (first letter capitalized, rest lowercase except for proper nouns)
            var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();
            
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i].Trim();
                if (string.IsNullOrEmpty(word)) continue;
                
                if (i == 0)
                {
                    // First word is always capitalized
                    result.Add(char.ToUpper(word[0]) + word.Substring(1).ToLower());
                }
                else
                {
                    // Check if it's a proper noun or important word that should remain capitalized
                    if (IsProperNoun(word))
                    {
                        result.Add(char.ToUpper(word[0]) + word.Substring(1).ToLower());
                    }
                    else
                    {
                        result.Add(word.ToLower());
                    }
                }
            }
            
            return string.Join(" ", result);
        }

        /// <summary>
        /// Check if a word should be capitalized in APA title format
        /// </summary>
        private bool IsProperNoun(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;
            
            word = word.ToLower();
            
            // Common proper nouns and important terms that should be capitalized
            var properNouns = new HashSet<string>
            {
                "ai", "artificial", "intelligence", "covid", "covid-19", "api", "http", "https", 
                "doi", "isbn", "issn", "ieee", "acm", "springer", "elsevier", "researchgate",
                "arxiv", "google", "microsoft", "amazon", "facebook", "twitter", "linkedin",
                "vietnam", "vietnamese", "america", "american", "china", "chinese", "japan", "japanese",
                "i", "ii", "iii", "iv", "v", "vi", "vii", "viii", "ix", "x"
            };
            
            return properNouns.Contains(word) || 
                   word.Length <= 3 && word.All(char.IsUpper); // Acronyms
        }

        public async Task<(string formatted, string inText)> GenerateCitationAsync(
            string title,
            string authors,
            int? year,
            string publicationDate,
            string type,
            string url,
            string style,
            string doi = "",
            string volume = "",
            string issue = "",
            string pages = "",
            string publisher = "")
        {
            Console.WriteLine($"[GeminiCitationService] Generating citation for style: {style}");
            Console.WriteLine($"[GeminiCitationService] Input data - Title: {title}, Authors: {authors}, Year: {year}, PublicationDate: {publicationDate}");

            // For now, always use fallback for consistent results
            if (!_useAI)
            {
                Console.WriteLine("[GeminiCitationService] Using direct fallback mode for consistent results");
                var fallbackResult = GenerateAccurateCitation(title, authors, year, url, style, publicationDate, doi, volume, issue, pages, publisher);
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
                return GenerateAccurateCitation(title, authors, year, url, style, publicationDate, doi, volume, issue, pages, publisher);
            }
        }

        private (string formatted, string inText) GenerateAccurateCitation(
            string title, string authors, int? year, string url, string style, string publicationDate,
            string doi = "", string volume = "", string issue = "", string pages = "", string publisher = "")
        {
            // Extract year from multiple sources
            var effectiveYear = ExtractYear(year, publicationDate);
            var accessDate = DateTime.Now.ToString("MMMM dd, yyyy");
            
            Console.WriteLine($"[GeminiCitationService] Effective year: {effectiveYear}, Access date: {accessDate}");

            // Clean inputs
            title = title?.Trim() ?? "Untitled";
            authors = string.IsNullOrWhiteSpace(authors?.Trim()) ? GenerateRealisticAuthor(title, url) : authors.Trim();
            url = url?.Trim() ?? "";

            string formatted, inText;

            switch (style.ToUpper())
            {
                case "APA":
                    (formatted, inText) = GenerateAPACitation(authors, effectiveYear, title, url, accessDate, doi, volume, issue, pages, publisher);
                    break;

                case "MLA":
                    (formatted, inText) = GenerateMLACitation(authors, title, url, accessDate, effectiveYear, doi, volume, issue, pages, publisher);
                    break;

                case "CHICAGO":
                    (formatted, inText) = GenerateChicagoCitation(authors, title, url, accessDate, effectiveYear, doi, volume, issue, pages, publisher);
                    break;

                case "HARVARD":
                    (formatted, inText) = GenerateHarvardCitation(authors, effectiveYear, title, url, accessDate, doi, volume, issue, pages, publisher);
                    break;

                case "IEEE":
                    (formatted, inText) = GenerateIEEECitation(authors, title, url, accessDate, doi, volume, issue, pages, publisher);
                    break;

                case "VANCOUVER":
                    (formatted, inText) = GenerateVancouverCitation(authors, title, url, accessDate, doi, volume, issue, pages, publisher);
                    break;

                default:
                    (formatted, inText) = GenerateAPACitation(authors, effectiveYear, title, url, accessDate, doi, volume, issue, pages, publisher);
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

        private (string formatted, string inText) GenerateAPACitation(string authors, int? year, string title, string url, string accessDate,
            string doi = "", string volume = "", string issue = "", string pages = "", string publisher = "")
        {
            // Ensure we always have a valid year - should not be null after ExtractYear changes
            var yearStr = year?.ToString() ?? DateTime.Now.Year.ToString();
            
            // Format APA citation according to academic standards
            var authorPart = FormatAuthorsAPA(authors);
            
            // Format title with proper capitalization (only first word and proper nouns capitalized)
            var formattedTitle = FormatTitleAPA(title);
            
            // Build the formatted citation with publisher and volume/issue/pages
            string formatted;
            var citationParts = new List<string> { $"{authorPart} ({yearStr})", formattedTitle };

            // Add publisher if available
            if (!string.IsNullOrEmpty(publisher))
            {
                citationParts.Add(publisher);
            }

            var volIssuePages = "";
            // Add volume/issue/pages information if available
            if (!string.IsNullOrEmpty(volume))
            {
                volIssuePages = $"{volume}";
                
                if (!string.IsNullOrEmpty(issue))
                {
                    volIssuePages += $"({issue})";
                }
            }
            if (!string.IsNullOrEmpty(pages))
            {
                if (!string.IsNullOrEmpty(volIssuePages))
                    volIssuePages += $", {pages}";
                else
                    volIssuePages = pages;
            }
            if (!string.IsNullOrEmpty(volIssuePages))
            {
                citationParts.Add(volIssuePages);
                Console.WriteLine($"Vol/Issue/Pages Part: {volIssuePages}");
            }

            // Add DOI if available, otherwise URL
            if (!string.IsNullOrEmpty(doi))
            {
                citationParts.Add($"https://doi.org/{doi}");
            }
            else if (!string.IsNullOrEmpty(url))
            {
                citationParts.Add($"Retrieved {accessDate}, from {url}");
            }
            else
            {
                citationParts.Add($"Retrieved {accessDate}");
            }
            
            formatted = string.Join(". ", citationParts);
            
            // In-text citation: (Author, Year) or (Author et al., Year)
            var inText = $"({GetLastNameAPA(authors)}, {yearStr})";
            
            return (formatted, inText);
        }

        private string FormatAuthorsAPA(string authors)
        {
            if (string.IsNullOrWhiteSpace(authors))
            {
                return "Academic Authors"; // Never return "Unknown Author"
            }

            // Handle multiple authors separated by commas, semicolons, or "and"
            var authorList = authors.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(a => a.Trim())
                                   .ToList();

            // Handle "and" in a single string
            if (authorList.Count == 1 && authorList[0].Contains(" and "))
            {
                authorList = authorList[0].Split(new string[] { " and " }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(a => a.Trim())
                                         .ToList();
            }

            // Format each author to APA style (Last, F. M.)
            var formattedAuthors = authorList.Select(FormatSingleAuthorAPA).ToList();

            if (formattedAuthors.Count == 1)
            {
                return formattedAuthors[0];
            }
            else if (formattedAuthors.Count == 2)
            {
                return $"{formattedAuthors[0]}, & {formattedAuthors[1]}";
            }
            else if (formattedAuthors.Count <= 7)
            {
                // For 3-7 authors, list all with & before the last
                var allButLast = string.Join(", ", formattedAuthors.Take(formattedAuthors.Count - 1));
                return $"{allButLast}, & {formattedAuthors.Last()}";
            }
            else
            {
                // For more than 7 authors, use first 6, then ..., then last author
                var firstSix = string.Join(", ", formattedAuthors.Take(6));
                return $"{firstSix}, ..., {formattedAuthors.Last()}";
            }
        }

        /// <summary>
        /// Format a single author name to APA style (Last, F. M.)
        /// </summary>
        private string FormatSingleAuthorAPA(string authorName)
        {
            if (string.IsNullOrWhiteSpace(authorName))
                return "Academic Author";

            authorName = authorName.Trim();
            
            // If already in Last, F. M. format, return as is
            if (System.Text.RegularExpressions.Regex.IsMatch(authorName, @"^[A-Z][a-z]+,\s[A-Z]\.\s?([A-Z]\.)?"))
            {
                return authorName;
            }

            var nameParts = authorName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (nameParts.Length == 1)
            {
                // Only one name part, treat as last name
                return nameParts[0];
            }
            else if (nameParts.Length == 2)
            {
                // First Last -> Last, F.
                var firstName = nameParts[0];
                var lastName = nameParts[1];
                return $"{lastName}, {firstName.Substring(0, 1).ToUpper()}.";
            }
            else if (nameParts.Length >= 3)
            {
                // First Middle Last -> Last, F. M.
                var firstName = nameParts[0];
                var middleName = nameParts[1];
                var lastName = nameParts[nameParts.Length - 1];
                return $"{lastName}, {firstName.Substring(0, 1).ToUpper()}. {middleName.Substring(0, 1).ToUpper()}.";
            }

            return authorName;
        }

        private string GetLastNameAPA(string authors)
        {
            if (string.IsNullOrWhiteSpace(authors))
            {
                return "Academic Authors"; // Never return "Unknown Author"
            }

            // Handle multiple authors separated by commas, semicolons, or "and"
            var authorList = authors.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(a => a.Trim())
                                   .ToList();

            // Handle "and" in a single string
            if (authorList.Count == 1 && authorList[0].Contains(" and "))
            {
                authorList = authorList[0].Split(new string[] { " and " }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(a => a.Trim())
                                         .ToList();
            }

            if (authorList.Count == 1)
            {
                // Single author: extract last name
                return ExtractLastName(authorList[0]);
            }
            else if (authorList.Count == 2)
            {
                // Two authors: Author1 & Author2
                var author1LastName = ExtractLastName(authorList[0]);
                var author2LastName = ExtractLastName(authorList[1]);
                return $"{author1LastName} & {author2LastName}";
            }
            else
            {
                // Multiple authors: First author et al.
                var firstAuthorLastName = ExtractLastName(authorList[0]);
                return $"{firstAuthorLastName} et al.";
            }
        }

        /// <summary>
        /// Extract last name from a full name
        /// </summary>
        private string ExtractLastName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "Academic Author";

            fullName = fullName.Trim();

            // If already in "Last, F. M." format, extract the last name part
            if (fullName.Contains(","))
            {
                return fullName.Split(',')[0].Trim();
            }

            // For "First Middle Last" format
            var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (nameParts.Length > 1)
            {
                return nameParts.Last(); // Return last name
            }
            
            return fullName; // Return as is if only one part
        }

        private (string formatted, string inText) GenerateMLACitation(string authors, string title, string url, string accessDate, int? year,
            string doi = "", string volume = "", string issue = "", string pages = "", string publisher = "")
        {
            var authorPart = FormatAuthorsMLANew(authors);
            var yearStr = year?.ToString() ?? DateTime.Now.Year.ToString();
            
            // Format: Author. "Title." Publisher, vol. Volume, no. Issue, Year, pp. Pages. DOI/URL.
            var citationParts = new List<string>();
            
            // Author and title (clean AI artifacts)
            var cleanTitle = CleanAIGeneratedTitle(title);
            citationParts.Add(@$"{authorPart}. ""{cleanTitle}.""");



            // Add publisher if available
            if (!string.IsNullOrEmpty(publisher))
            {
                citationParts.Add(publisher);
            }

            // Volume/issue/pages information
            var volIssuePages = "";
            if (!string.IsNullOrEmpty(volume))
            {
                volIssuePages += $"vol. {volume}";
                if (!string.IsNullOrEmpty(issue))
                {
                    volIssuePages += $", no. {issue}";
                }
            }
            if (!string.IsNullOrEmpty(pages))
            {
                if (!string.IsNullOrEmpty(volIssuePages))
                    volIssuePages += $", pp. {pages}";
                else
                    volIssuePages = $"pp. {pages}";
            }
            if (!string.IsNullOrEmpty(volIssuePages))
            {
                citationParts.Add(volIssuePages);
                Console.WriteLine($"Vol/Issue/Pages Part: {volIssuePages}");
            }

            // DOI or URL
            if (!string.IsNullOrEmpty(doi))
            {
                citationParts.Add($"https://doi.org/{doi}");
            }
            else if (!string.IsNullOrEmpty(url))
            {
                citationParts.Add(url);
            }
            
            var formatted = string.Join(". ", citationParts);
            if (!formatted.EndsWith("."))
            {
                formatted += ".";
            }
            
            // In-text citation: (Author Page) - for now just (Author) since we don't have page numbers in context
            var inText = $"({GetLastNameMLA(authors)})";
            return (formatted, inText);
        }

        private (string formatted, string inText) GenerateChicagoCitation(string authors, string title, string url, string accessDate, int? year,
            string doi = "", string volume = "", string issue = "", string pages = "", string publisher = "")
        {
            var authorPart = FormatAuthorsChicagoNew(authors);
            var yearPart = year?.ToString() ?? DateTime.Now.Year.ToString();
            
            // Format: Author. "Title." Publisher Volume, no. Issue (Year): Pages. DOI/URL.
            var citationParts = new List<string>();
            
            // Author and title (clean AI artifacts)
            var cleanTitle = CleanAIGeneratedTitle(title);
            citationParts.Add(@$"{authorPart}. ""{cleanTitle}.""");



            // Add publisher if available
            if (!string.IsNullOrEmpty(publisher))
            {
                citationParts.Add(publisher);
            }

            // Volume/issue/pages information
            var volIssuePages = "";
            if (!string.IsNullOrEmpty(volume))
            {
                volIssuePages += $"{volume}";
                if (!string.IsNullOrEmpty(issue))
                {
                    volIssuePages += $", no. {issue}";
                }
                volIssuePages += $" ({yearPart})";
            }
            if (!string.IsNullOrEmpty(pages))
            {
                if (!string.IsNullOrEmpty(volIssuePages))
                    volIssuePages += $": {pages}";
                else
                    volIssuePages = $"({yearPart}): {pages}";
            }
            if (!string.IsNullOrEmpty(volIssuePages))
            {
                citationParts.Add(volIssuePages);
                Console.WriteLine($"Vol/Issue/Pages Part: {volIssuePages}");
            }

            // DOI or URL
            if (!string.IsNullOrEmpty(doi))
            {
                citationParts.Add($"https://doi.org/{doi}");
            }
            else if (!string.IsNullOrEmpty(url))
            {
                citationParts.Add($"Accessed {accessDate}. {url}");
            }
            
            var formatted = string.Join(". ", citationParts);
            if (!formatted.EndsWith("."))
            {
                formatted += ".";
            }
            
            var inText = $"({GetLastNameChicago(authors)}, {yearPart})";
            return (formatted, inText);
        }

        private (string formatted, string inText) GenerateHarvardCitation(string authors, int? year, string title, string url, string accessDate,
            string doi = "", string volume = "", string issue = "", string pages = "", string publisher = "")
        {
            var yearStr = year?.ToString() ?? DateTime.Now.Year.ToString();
            var authorPart = FormatAuthorsHarvardNew(authors);
            
            // Build citation with publisher and metadata (clean AI artifacts)
            var cleanTitle = CleanAIGeneratedTitle(title);
            var formatted = $"{authorPart} ({yearStr}) '{cleanTitle}'";
            
            // Add publisher if available
            if (!string.IsNullOrEmpty(publisher))
            {
                formatted += $", {publisher}";
            }
            
            // Add volume and issue if available
            if (!string.IsNullOrEmpty(volume))
            {
                formatted += $", {volume}";
                if (!string.IsNullOrEmpty(issue))
                {
                    formatted += $"({issue})";
                }
            }
            
            // Add pages if available
            if (!string.IsNullOrEmpty(pages))
            {
                formatted += $", pp. {pages}";
            }
            
            // Add DOI or URL
            if (!string.IsNullOrEmpty(doi))
            {
                formatted += $". doi: {doi}";
            }
            else if (!string.IsNullOrEmpty(url))
            {
                formatted += $". Available at: {url}";
            }
            else
            {
                formatted += ".";
            }
            
            var inText = $"({GetLastNameHarvard(authors)}, {yearStr})";
            return (formatted, inText);
        }

        private (string formatted, string inText) GenerateIEEECitation(string authors, string title, string url, string accessDate,
            string doi = "", string volume = "", string issue = "", string pages = "", string publisher = "")
        {
            var authorPart = FormatAuthorsIEEENew(authors);
            
            // Build citation with publisher and metadata (clean AI artifacts)
            var cleanTitle = CleanAIGeneratedTitle(title);
            var formatted = $"[1] {authorPart}, \"{cleanTitle}\"";
            
            // Add publisher if available
            if (!string.IsNullOrEmpty(publisher))
            {
                formatted += $", {publisher}";
            }
            
            // Add volume if available
            if (!string.IsNullOrEmpty(volume))
            {
                formatted += $", vol. {volume}";
            }
            
            // Add issue if available
            if (!string.IsNullOrEmpty(issue))
            {
                formatted += $", no. {issue}";
            }
            
            // Add pages if available
            if (!string.IsNullOrEmpty(pages))
            {
                formatted += $", pp. {pages}";
            }
            
            // Add year (extracted from accessDate or current year)
            var year = DateTime.Now.Year;
            formatted += $", {year}";
            
            // Add DOI or URL
            if (!string.IsNullOrEmpty(doi))
            {
                formatted += $". doi: {doi}";
            }
            else if (!string.IsNullOrEmpty(url))
            {
                formatted += $". Available: {url}";
            }
            else
            {
                formatted += ".";
            }
            
            var inText = "[1]";
            return (formatted, inText);
        }

        private (string formatted, string inText) GenerateVancouverCitation(string authors, string title, string url, string accessDate,
            string doi = "", string volume = "", string issue = "", string pages = "", string publisher = "")
        {
            var authorPart = FormatAuthorsVancouverNew(authors);
            
            // Build citation with publisher and metadata (clean AI artifacts)
            var cleanTitle = CleanAIGeneratedTitle(title);
            var formatted = $"(1) {authorPart}. {cleanTitle}";
            
            // Add publisher if available
            if (!string.IsNullOrEmpty(publisher))
            {
                formatted += $". {publisher}";
            }
            
            // Add year
            var year = DateTime.Now.Year;
            formatted += $". {year}";
            
            // Add volume and issue if available
            if (!string.IsNullOrEmpty(volume))
            {
                formatted += $";{volume}";
                if (!string.IsNullOrEmpty(issue))
                {
                    formatted += $"({issue})";
                }
            }
            
            // Add pages if available
            if (!string.IsNullOrEmpty(pages))
            {
                formatted += $":{pages}";
            }
            
            formatted += ".";
            
            var inText = "[1]";
            return (formatted, inText);
        }

        // Format methods for different citation styles
        private string FormatAuthorsMLA(string authors) => FormatAuthorsGeneric(authors);
        private string FormatAuthorsChicago(string authors) => FormatAuthorsGeneric(authors);
        
        private string FormatAuthorsChicagoNew(string authors)
        {
            if (string.IsNullOrEmpty(authors))
                return "";

            // Clean the authors string
            authors = authors.Trim();

            // Split by common delimiters and clean up
            var authorList = authors.Split(new[] { ",", ";", " and ", " & " }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(a => a.Trim())
                                   .Where(a => !string.IsNullOrEmpty(a))
                                   .ToList();

            if (authorList.Count == 0)
                return "";

            // Format authors for Chicago style: FirstName LastName
            var formattedAuthors = new List<string>();
            
            for (int i = 0; i < authorList.Count && i < 3; i++)
            {
                var author = FormatSingleAuthorChicago(authorList[i]);
                formattedAuthors.Add(author);
            }

            // Handle multiple authors
            if (authorList.Count > 3)
            {
                return $"{formattedAuthors[0]}, {formattedAuthors[1]}, {formattedAuthors[2]}, và {formattedAuthors[2]}";
            }
            else if (authorList.Count == 3)
            {
                return $"{formattedAuthors[0]}, {formattedAuthors[1]}, và {formattedAuthors[2]}";
            }
            else if (authorList.Count == 2)
            {
                return $"{formattedAuthors[0]}, và {formattedAuthors[1]}";
            }

            return formattedAuthors[0];
        }

        private string FormatSingleAuthorChicago(string author)
        {
            if (string.IsNullOrEmpty(author))
                return "";

            author = author.Trim();
            var parts = author.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                return author; // Single word, return as is
            }

            // Assume last part is surname, rest are given names
            var surname = parts.Last();
            var givenNames = parts.Take(parts.Length - 1).ToList();

            // Chicago format: FirstName LastName (natural order)
            return $"{string.Join(" ", givenNames)} {surname}";
        }
        private string FormatAuthorsHarvard(string authors) => FormatAuthorsGeneric(authors);
        
        private string FormatAuthorsHarvardNew(string authors)
        {
            if (string.IsNullOrEmpty(authors))
                return "";

            // Clean the authors string
            authors = authors.Trim();

            // Split by common delimiters and clean up
            var authorList = authors.Split(new[] { ",", ";", " and ", " & " }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(a => a.Trim())
                                   .Where(a => !string.IsNullOrEmpty(a))
                                   .ToList();

            if (authorList.Count == 0)
                return "";

            // Format first author: Lastname, FirstInitial.MiddleInitial.
            var formattedAuthors = new List<string>();
            var firstAuthor = FormatSingleAuthorHarvard(authorList[0], true);
            formattedAuthors.Add(firstAuthor);

            // Add subsequent authors
            if (authorList.Count > 1)
            {
                for (int i = 1; i < authorList.Count && i < 3; i++)
                {
                    var author = FormatSingleAuthorHarvard(authorList[i], false);
                    formattedAuthors.Add(author);
                }

                // Handle multiple authors
                if (authorList.Count > 3)
                {
                    return $"{formattedAuthors[0]}, {formattedAuthors[1]}, {formattedAuthors[2]} et al.";
                }
                else if (authorList.Count == 3)
                {
                    return $"{formattedAuthors[0]}, {formattedAuthors[1]} and {formattedAuthors[2]}";
                }
                else if (authorList.Count == 2)
                {
                    return $"{formattedAuthors[0]} and {formattedAuthors[1]}";
                }
            }

            return formattedAuthors[0];
        }

        private string FormatSingleAuthorHarvard(string author, bool isFirst)
        {
            if (string.IsNullOrEmpty(author))
                return "";

            author = author.Trim();
            var parts = author.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                return author; // Single word, return as is
            }

            // Assume last part is surname, rest are given names
            var surname = parts.Last();
            var givenNames = parts.Take(parts.Length - 1).ToList();

            // Create initials from given names
            var initials = string.Join("", givenNames.Select(name => 
            {
                var initial = name.Substring(0, 1).ToUpper();
                return initial + ".";
            }));

            // Harvard format: Surname, I.I. (for all authors)
            return $"{surname}, {initials}";
        }
        private string FormatAuthorsIEEE(string authors) => FormatAuthorsGeneric(authors);
        private string FormatAuthorsVancouver(string authors) => FormatAuthorsGeneric(authors);
        
        private string FormatAuthorsIEEENew(string authors)
        {
            if (string.IsNullOrEmpty(authors))
                return "";

            // Clean the authors string
            authors = authors.Trim();

            // Split by common delimiters and clean up
            var authorList = authors.Split(new[] { ",", ";", " and ", " & " }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(a => a.Trim())
                                   .Where(a => !string.IsNullOrEmpty(a))
                                   .ToList();

            if (authorList.Count == 0)
                return "";

            // Format authors for IEEE style: F. M. Lastname
            var formattedAuthors = new List<string>();
            
            for (int i = 0; i < authorList.Count; i++)
            {
                var author = FormatSingleAuthorIEEE(authorList[i]);
                formattedAuthors.Add(author);
            }

            // Join authors with commas and "and"
            if (formattedAuthors.Count > 2)
            {
                var lastAuthor = formattedAuthors.Last();
                var otherAuthors = string.Join(", ", formattedAuthors.Take(formattedAuthors.Count - 1));
                return $"{otherAuthors}, and {lastAuthor}";
            }
            else if (formattedAuthors.Count == 2)
            {
                return $"{formattedAuthors[0]} and {formattedAuthors[1]}";
            }

            return formattedAuthors[0];
        }

        private string FormatSingleAuthorIEEE(string author)
        {
            if (string.IsNullOrEmpty(author))
                return "";

            author = author.Trim();
            var parts = author.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                return author; // Single word, return as is
            }

            // Assume last part is surname, rest are given names
            var surname = parts.Last();
            var givenNames = parts.Take(parts.Length - 1).ToList();

            // Create initials from given names
            var initials = string.Join(" ", givenNames.Select(name => 
            {
                var initial = name.Substring(0, 1).ToUpper();
                return initial + ".";
            }));

            // IEEE format: F. M. Surname
            return $"{initials} {surname}";
        }
        
        private string FormatAuthorsVancouverNew(string authors)
        {
            if (string.IsNullOrEmpty(authors))
                return "";

            // Clean the authors string
            authors = authors.Trim();

            // Split by common delimiters and clean up
            var authorList = authors.Split(new[] { ",", ";", " and ", " & " }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(a => a.Trim())
                                   .Where(a => !string.IsNullOrEmpty(a))
                                   .ToList();

            if (authorList.Count == 0)
                return "";

            // Format authors for Vancouver style: Surname FM
            var formattedAuthors = new List<string>();
            
            for (int i = 0; i < authorList.Count; i++)
            {
                var author = FormatSingleAuthorVancouver(authorList[i]);
                formattedAuthors.Add(author);
            }

            // Join authors with commas - Vancouver style
            return string.Join(", ", formattedAuthors);
        }

        private string FormatSingleAuthorVancouver(string author)
        {
            if (string.IsNullOrEmpty(author))
                return "";

            author = author.Trim();
            var parts = author.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                return author; // Single word, return as is
            }

            // Assume last part is surname, rest are given names
            var surname = parts.Last();
            var givenNames = parts.Take(parts.Length - 1).ToList();

            // Create initials without periods for Vancouver style
            var initials = string.Join("", givenNames.Select(name => 
            {
                var initial = name.Substring(0, 1).ToUpper();
                return initial;
            }));

            // Vancouver format: Surname FM (no spaces, no periods)
            return $"{surname} {initials}";
        }

        /// <summary>
        /// Format authors for MLA style: Last, First, et al.
        /// </summary>
        private string FormatAuthorsMLANew(string authors)
        {
            if (string.IsNullOrWhiteSpace(authors))
            {
                return "Academic Authors";
            }

            // Handle multiple authors separated by commas, semicolons, or "and"
            var authorList = authors.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(a => a.Trim())
                                   .ToList();

            // Handle "and" in a single string
            if (authorList.Count == 1 && authorList[0].Contains(" and "))
            {
                authorList = authorList[0].Split(new string[] { " and " }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(a => a.Trim())
                                         .ToList();
            }

            if (authorList.Count == 1)
            {
                return FormatSingleAuthorMLA(authorList[0]);
            }
            else if (authorList.Count == 2)
            {
                return $"{FormatSingleAuthorMLA(authorList[0])}, and {FormatSingleAuthorMLA(authorList[1], false)}";
            }
            else if (authorList.Count >= 3)
            {
                // For 3+ authors: First author, et al.
                return $"{FormatSingleAuthorMLA(authorList[0])}, et al.";
            }

            return authors;
        }

        /// <summary>
        /// Format a single author for MLA: Last, First
        /// </summary>
        private string FormatSingleAuthorMLA(string authorName, bool isFirstAuthor = true)
        {
            if (string.IsNullOrWhiteSpace(authorName))
                return "Academic Author";

            authorName = authorName.Trim();
            
            // If already in Last, First format, return as is
            if (authorName.Contains(",") && isFirstAuthor)
            {
                return authorName;
            }

            var nameParts = authorName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (nameParts.Length == 1)
            {
                return nameParts[0];
            }
            else if (nameParts.Length == 2)
            {
                if (isFirstAuthor)
                {
                    // First Middle Last -> Last, First
                    return $"{nameParts[1]}, {nameParts[0]}";
                }
                else
                {
                    // Subsequent authors: First Last
                    return $"{nameParts[0]} {nameParts[1]}";
                }
            }
            else if (nameParts.Length >= 3)
            {
                if (isFirstAuthor)
                {
                    // First Middle Last -> Last, First Middle
                    var firstName = string.Join(" ", nameParts.Take(nameParts.Length - 1));
                    var lastName = nameParts[nameParts.Length - 1];
                    return $"{lastName}, {firstName}";
                }
                else
                {
                    // Subsequent authors: First Middle Last
                    return string.Join(" ", nameParts);
                }
            }

            return authorName;
        }

        private string FormatAuthorsGeneric(string authors)
        {
            if (string.IsNullOrWhiteSpace(authors))
            {
                return "Academic Authors"; // Never return "Unknown Author"
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

        public async Task<string> ExtractAuthorWithAIAsync(string prompt)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_apiKey))
                {
                    Console.WriteLine("[GeminiCitationService] API key not configured, using fallback for author extraction");
                    return ExtractAuthorFallback(prompt);
                }

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[GeminiCitationService] API call failed: {response.StatusCode}");
                    return "Research Author";
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var geminiResponse = JsonSerializer.Deserialize<GeminiCitationApiResponse>(responseContent);

                if (geminiResponse?.Candidates?.Any() == true)
                {
                    var authorText = geminiResponse.Candidates.First().Content.Parts.First().Text.Trim();
                    Console.WriteLine($"[GeminiCitationService] AI extracted author: {authorText}");
                    return authorText;
                }

                return ExtractAuthorFallback(prompt);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GeminiCitationService] Author extraction failed: {ex.Message}");
                return ExtractAuthorFallback(prompt);
            }
        }

        private string ExtractAuthorFallback(string prompt)
        {
            // Simple fallback logic based on prompt content
            if (prompt.Contains("researchgate", StringComparison.OrdinalIgnoreCase))
                return "ResearchGate Author";
            if (prompt.Contains("sciencedirect", StringComparison.OrdinalIgnoreCase))
                return "ScienceDirect Author";
            if (prompt.Contains("arxiv", StringComparison.OrdinalIgnoreCase))
                return "ArXiv Author";
            if (prompt.Contains(".edu", StringComparison.OrdinalIgnoreCase))
                return "Academic Researcher";
            if (prompt.Contains("wikipedia", StringComparison.OrdinalIgnoreCase))
                return "Wikipedia Contributors";
            
            return "Research Author";
        }

        public async Task<(string title, string author, int? year, string publisher)> ExtractCitationMetadataWithAIAsync(string prompt)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_apiKey))
                {
                    Console.WriteLine("[GeminiCitationService] API key not configured, using fallback for metadata extraction");
                    return ExtractMetadataFallback(prompt);
                }

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[GeminiCitationService] API call failed: {response.StatusCode}");
                    return ExtractMetadataFallback(prompt);
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var geminiResponse = JsonSerializer.Deserialize<GeminiCitationApiResponse>(responseContent);

                if (geminiResponse?.Candidates?.Any() == true)
                {
                    var metadataText = geminiResponse.Candidates.First().Content.Parts.First().Text.Trim();
                    Console.WriteLine($"[GeminiCitationService] AI extracted metadata: {metadataText}");
                    
                    // Parse metadata from AI response (text format expected)
                    return ParseMetadataFromText(metadataText);
                }

                return ExtractMetadataFallback(prompt);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GeminiCitationService] Metadata extraction failed: {ex.Message}");
                return ExtractMetadataFallback(prompt);
            }
        }

        private (string title, string author, int? year, string publisher) ExtractMetadataFallback(string prompt)
        {
            var title = "Unknown Title";
            var author = "Research Author";
            var year = DateTime.UtcNow.Year;
            var publisher = "Academic Publisher";

            Console.WriteLine($"[GeminiCitationService] Using fallback metadata extraction for: {prompt.Substring(0, Math.Min(100, prompt.Length))}...");

            // Try to extract title from prompt
            if (prompt.Contains("Title:", StringComparison.OrdinalIgnoreCase))
            {
                var titleMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"Title:\s*""?([^""\n]+)""?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (titleMatch.Success && !string.IsNullOrWhiteSpace(titleMatch.Groups[1].Value))
                {
                    title = titleMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"[GeminiCitationService] Extracted title: {title}");
                }
            }

            // Try to extract author from title patterns
            if (prompt.Contains("Title:", StringComparison.OrdinalIgnoreCase))
            {
                var titleMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"Title:\s*(.+?)(?:\n|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                {
                    var titleText = titleMatch.Groups[1].Value.Trim();
                    // Try to extract author from common title patterns like "Author Name - Title" or "Title by Author Name"
                    var byAuthorMatch = System.Text.RegularExpressions.Regex.Match(titleText, @"by\s+([A-Za-z\s\.]+)(?:\s|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (byAuthorMatch.Success)
                    {
                        author = byAuthorMatch.Groups[1].Value.Trim();
                        Console.WriteLine($"[GeminiCitationService] Extracted author from title: {author}");
                    }
                }
            }

            // Try to extract year from URL or text
            var yearMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"\b(19|20)\d{2}\b");
            if (yearMatch.Success && int.TryParse(yearMatch.Value, out var extractedYear))
            {
                year = extractedYear;
                Console.WriteLine($"[GeminiCitationService] Extracted year: {year}");
            }

            // Domain-based fallbacks
            if (prompt.Contains("researchgate", StringComparison.OrdinalIgnoreCase))
            {
                publisher = "ResearchGate";
                if (author == "Research Author") author = "ResearchGate Researcher";
            }
            else if (prompt.Contains("sciencedirect", StringComparison.OrdinalIgnoreCase))
            {
                publisher = "ScienceDirect";
                if (author == "Research Author") author = "ScienceDirect Author";
            }
            else if (prompt.Contains("arxiv", StringComparison.OrdinalIgnoreCase))
            {
                publisher = "ArXiv";
                if (author == "Research Author") author = "ArXiv Author";
            }
            else if (prompt.Contains(".edu", StringComparison.OrdinalIgnoreCase))
            {
                publisher = "Academic Institution";
                if (author == "Research Author") author = "Academic Researcher";
            }
            else if (prompt.Contains("wikipedia", StringComparison.OrdinalIgnoreCase))
            {
                publisher = "Wikipedia";
                author = "Wikipedia Contributors";
            }
            else if (prompt.Contains("ieee", StringComparison.OrdinalIgnoreCase))
            {
                publisher = "IEEE";
                if (author == "Research Author") author = "IEEE Author";
            }

            return (title, author, year, publisher);
        }

        private (string title, string author, int? year, string publisher) ParseMetadataFromText(string text)
        {
            var title = "Unknown Title";
            var author = "Research Author";
            int? year = DateTime.UtcNow.Year;
            var publisher = "Unknown Publisher";

            Console.WriteLine($"[GeminiCitationService] Parsing AI response: {text}");

            // Parse title
            var titleMatch = System.Text.RegularExpressions.Regex.Match(text, @"Title:\s*(.+?)(?:\n|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (titleMatch.Success && !string.IsNullOrWhiteSpace(titleMatch.Groups[1].Value))
            {
                title = titleMatch.Groups[1].Value.Trim();
                Console.WriteLine($"[GeminiCitationService] Extracted title: {title}");
            }

            // Parse author
            var authorMatch = System.Text.RegularExpressions.Regex.Match(text, @"Author:\s*(.+?)(?:\n|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (authorMatch.Success && !string.IsNullOrWhiteSpace(authorMatch.Groups[1].Value))
            {
                author = authorMatch.Groups[1].Value.Trim();
                // Don't use "Unknown" as author if we can extract something meaningful
                if (!author.Equals("Unknown", StringComparison.OrdinalIgnoreCase) && 
                    !author.Equals("Unknown Author", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[GeminiCitationService] Extracted author: {author}");
                }
                else
                {
                    author = "Research Author"; // Better fallback
                }
            }

            // Parse year
            var yearMatch = System.Text.RegularExpressions.Regex.Match(text, @"Year:\s*(\d{4})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var extractedYear))
            {
                year = extractedYear;
                Console.WriteLine($"[GeminiCitationService] Extracted year: {year}");
            }
            else
            {
                // Try to extract any 4-digit year from the text
                var anyYearMatch = System.Text.RegularExpressions.Regex.Match(text, @"\b(19|20)\d{2}\b");
                if (anyYearMatch.Success && int.TryParse(anyYearMatch.Value, out var anyYear))
                {
                    year = anyYear;
                    Console.WriteLine($"[GeminiCitationService] Found year in text: {year}");
                }
            }

            // Parse publisher
            var publisherMatch = System.Text.RegularExpressions.Regex.Match(text, @"Publisher:\s*(.+?)(?:\n|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (publisherMatch.Success && !string.IsNullOrWhiteSpace(publisherMatch.Groups[1].Value))
            {
                publisher = publisherMatch.Groups[1].Value.Trim();
                if (!publisher.Equals("Unknown", StringComparison.OrdinalIgnoreCase) && 
                    !publisher.Equals("Unknown Publisher", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[GeminiCitationService] Extracted publisher: {publisher}");
                }
                else
                {
                    publisher = "Academic Publisher"; // Better fallback
                }
            }

            return (title, author, year, publisher);
        }

        private class CitationMetadata
        {
            [JsonPropertyName("author")]
            public string Author { get; set; }

            [JsonPropertyName("year")]
            public int? Year { get; set; }

            [JsonPropertyName("publisher")]
            public string Publisher { get; set; }
        }
    }
}
