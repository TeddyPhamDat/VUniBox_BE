using HtmlAgilityPack;
using VUniBox.Models.Enum;
using System.Text.RegularExpressions;
using VUniBox.Models.DTO;

namespace VUniBox.Services.Metadata
{
    public class UrlMetadataExtractor : IUrlMetadataExtractor
    {
        private readonly HttpClient _httpClient;

        public UrlMetadataExtractor(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<DocumentMetadataDto> ExtractMetadataAsync(string url, DocumentType documentType)
        {
            try
            {
                switch (documentType)
                {
                    case DocumentType.Research:
                        return await ExtractFromResearchSiteAsync(url);
                    case DocumentType.Book:
                        return await ExtractFromBookSiteAsync(url);
                    case DocumentType.Newspaper:
                        return await ExtractFromNewsSiteAsync(url);
                    default:
                        return await ExtractGenericMetadataAsync(url);
                }
            }
            catch (Exception ex)
            {
                return new DocumentMetadataDto
                {
                    URL = url,
                    Title = "Error extracting metadata",
                    Description = $"Failed to extract metadata: {ex.Message}"
                };
            }
        }

        public async Task<DocumentMetadataDto> ExtractFromResearchSiteAsync(string url)
        {
            var metadata = await ExtractGenericMetadataAsync(url);
            
            try
            {
                var html = await GetPageContentAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Extract DOI
                var doiMatch = Regex.Match(html, @"10\.\d{4,}/[^\s<>""']+", RegexOptions.IgnoreCase);
                if (doiMatch.Success)
                {
                    metadata.DOI = doiMatch.Value;
                }

                // Extract journal information
                var journalSelectors = new[]
                {
                    "meta[name='citation_journal_title']",
                    "meta[property='og:site_name']",
                    ".journal-title",
                    ".publication-title"
                };

                foreach (var selector in journalSelectors)
                {
                    var journalElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (journalElement != null)
                    {
                        metadata.Journal = GetAttributeValue(journalElement, "content") ?? journalElement.InnerText?.Trim();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                metadata.Description = $"Error extracting research metadata: {ex.Message}";
            }

            return metadata;
        }

        public async Task<DocumentMetadataDto> ExtractFromBookSiteAsync(string url)
        {
            var metadata = await ExtractGenericMetadataAsync(url);
            
            try
            {
                var html = await GetPageContentAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Extract ISBN for ScienceDirect and academic sites
                var isbnPatterns = new[]
                {
                    @"ISBN[:\s]*([0-9\-X]{10,17})",
                    @"isbn[:\s]*([0-9\-X]{10,17})",
                    @"\b(97[89][\d\-]{10,})\b"
                };

                foreach (var pattern in isbnPatterns)
                {
                    var isbnMatch = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                    if (isbnMatch.Success)
                    {
                        metadata.ISBN = isbnMatch.Groups[1].Value.Replace("-", "");
                        break;
                    }
                }

                // Extract DOI
                var doiPatterns = new[]
                {
                    @"(?:DOI[:\s]*|doi[:\s]*|https://doi\.org/)(10\.\d{4,}/[^\s<>""'\]]+)",
                    @"(10\.\d{4,}/[^\s<>""'\]]+)"
                };

                foreach (var pattern in doiPatterns)
                {
                    var doiMatch = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                    if (doiMatch.Success)
                    {
                        metadata.DOI = doiMatch.Groups[doiMatch.Groups.Count - 1].Value;
                        break;
                    }
                }

                // Enhanced publisher extraction
                var publisherSelectors = new[]
                {
                    "meta[name='citation_publisher']",
                    "meta[property='book:publisher']",
                    "meta[name='publisher']",
                    "meta[property='og:site_name']",
                    ".publisher",
                    ".imprint"
                };

                foreach (var selector in publisherSelectors)
                {
                    var publisherElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (publisherElement != null)
                    {
                        var publisher = GetAttributeValue(publisherElement, "content") ?? publisherElement.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(publisher) && publisher.Length > 2)
                        {
                            metadata.Publisher = publisher;
                            break;
                        }
                    }
                }

                // Enhanced author extraction
                var authorSelectors = new[]
                {
                    "meta[name='citation_author']",
                    "meta[property='book:author']",
                    "meta[name='author']",
                    "meta[property='author']",
                    ".author",
                    ".authors"
                };

                var authorsList = new List<string>();
                foreach (var selector in authorSelectors)
                {
                    var elements = doc.DocumentNode.SelectNodes($"//{selector}");
                    if (elements != null)
                    {
                        foreach (var element in elements)
                        {
                            var author = GetAttributeValue(element, "content") ?? element.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(author) && author.Length > 2)
                            {
                                author = Regex.Replace(author, @"^(By\s|Author[:\s]*)", "", RegexOptions.IgnoreCase).Trim();
                                if (!authorsList.Contains(author))
                                {
                                    authorsList.Add(author);
                                }
                            }
                        }
                    }
                }

                if (authorsList.Any())
                {
                    metadata.Authors = string.Join(", ", authorsList);
                    metadata.Author = authorsList.First();
                }

                // Publication date extraction
                var dateSelectors = new[]
                {
                    "meta[name='citation_publication_date']",
                    "meta[name='citation_date']",
                    "meta[property='book:release_date']",
                    ".publication-date"
                };

                foreach (var selector in dateSelectors)
                {
                    var dateElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (dateElement != null)
                    {
                        var dateStr = GetAttributeValue(dateElement, "content") ?? dateElement.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(dateStr))
                        {
                            if (DateTime.TryParse(dateStr, out var date))
                            {
                                metadata.PublicationDate = DateOnly.FromDateTime(date);
                                break;
                            }
                            if (Regex.IsMatch(dateStr, @"\b(19|20)\d{2}\b") && int.TryParse(Regex.Match(dateStr, @"\b(19|20)\d{2}\b").Value, out var year))
                            {
                                metadata.PublicationDate = new DateOnly(year, 1, 1);
                                break;
                            }
                        }
                    }
                }

                // Set language and retrieved date
                if (string.IsNullOrEmpty(metadata.Language))
                {
                    metadata.Language = "en";
                }
                metadata.RetrievedDate = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                metadata.Description = $"Error extracting book metadata: {ex.Message}";
            }

            return metadata;
        }

        public async Task<DocumentMetadataDto> ExtractFromNewsSiteAsync(string url)
        {
            var metadata = await ExtractGenericMetadataAsync(url);
            
            try
            {
                var html = await GetPageContentAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Extract publication date
                var dateSelectors = new[]
                {
                    "meta[property='article:published_time']",
                    "meta[name='article:published_time']",
                    "time[datetime]",
                    ".publish-date"
                };

                foreach (var selector in dateSelectors)
                {
                    var dateElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (dateElement != null)
                    {
                        var dateStr = GetAttributeValue(dateElement, "content") ?? 
                                     GetAttributeValue(dateElement, "datetime") ?? 
                                     dateElement.InnerText?.Trim();
                        
                        if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date))
                        {
                            metadata.PublicationDate = DateOnly.FromDateTime(date);
                            break;
                        }
                    }
                }

                // Extract author
                var authorSelectors = new[]
                {
                    "meta[name='article:author']",
                    "meta[property='article:author']",
                    "meta[name='author']",
                    ".author",
                    ".byline"
                };

                foreach (var selector in authorSelectors)
                {
                    var authorElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (authorElement != null)
                    {
                        var author = GetAttributeValue(authorElement, "content") ?? authorElement.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(author))
                        {
                            author = Regex.Replace(author, @"^(By\s|Written by\s)", "", RegexOptions.IgnoreCase).Trim();
                            metadata.Author = author;
                            metadata.Authors = author;
                            break;
                        }
                    }
                }

                // Extract publisher/source
                var publisherElement = doc.DocumentNode.SelectSingleNode("//meta[@property='og:site_name']");
                if (publisherElement != null)
                {
                    var publisher = GetAttributeValue(publisherElement, "content");
                    if (!string.IsNullOrEmpty(publisher))
                    {
                        metadata.Publisher = publisher;
                        metadata.Source = publisher;
                    }
                }

                // Set language and retrieved date
                if (string.IsNullOrEmpty(metadata.Language))
                {
                    metadata.Language = "en";
                }
                metadata.RetrievedDate = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                metadata.Description = $"Error extracting news metadata: {ex.Message}";
            }

            return metadata;
        }

        public async Task<DocumentMetadataDto> ExtractGenericMetadataAsync(string url)
        {
            var metadata = new DocumentMetadataDto { URL = url };

            try
            {
                var html = await GetPageContentAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Extract title
                var titleSelectors = new[]
                {
                    "meta[property='og:title']",
                    "meta[name='twitter:title']",
                    "title",
                    "h1"
                };

                foreach (var selector in titleSelectors)
                {
                    var titleElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (titleElement != null)
                    {
                        var title = GetAttributeValue(titleElement, "content") ?? titleElement.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(title) && title.Length > 5)
                        {
                            metadata.Title = title;
                            break;
                        }
                    }
                }

                // Extract description
                var descriptionSelectors = new[]
                {
                    "meta[property='og:description']",
                    "meta[name='description']",
                    "meta[name='twitter:description']"
                };

                foreach (var selector in descriptionSelectors)
                {
                    var descElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (descElement != null)
                    {
                        var description = GetAttributeValue(descElement, "content");
                        if (!string.IsNullOrEmpty(description))
                        {
                            metadata.Description = description;
                            metadata.Abstract = description;
                            break;
                        }
                    }
                }

                // Extract site name/source
                var siteElement = doc.DocumentNode.SelectSingleNode("//meta[@property='og:site_name']");
                if (siteElement != null)
                {
                    metadata.Source = GetAttributeValue(siteElement, "content");
                    if (string.IsNullOrEmpty(metadata.Publisher))
                    {
                        metadata.Publisher = metadata.Source;
                    }
                }

                // Extract author
                var authorElement = doc.DocumentNode.SelectSingleNode("//meta[@name='author']");
                if (authorElement != null)
                {
                    var author = GetAttributeValue(authorElement, "content");
                    if (!string.IsNullOrEmpty(author))
                    {
                        metadata.Author = author;
                        metadata.Authors = author;
                    }
                }

                // Extract keywords
                var keywordsElement = doc.DocumentNode.SelectSingleNode("//meta[@name='keywords']");
                if (keywordsElement != null)
                {
                    metadata.Keywords = GetAttributeValue(keywordsElement, "content");
                }

                // Extract language
                var langElement = doc.DocumentNode.SelectSingleNode("//html[@lang]");
                metadata.Language = langElement != null ? GetAttributeValue(langElement, "lang") ?? "en" : "en";

                // Set retrieved date
                metadata.RetrievedDate = DateTime.UtcNow;

                // Fallback for title
                if (string.IsNullOrEmpty(metadata.Title))
                {
                    var uri = new Uri(url);
                    metadata.Title = $"Document from {uri.Host}";
                }
            }
            catch (Exception ex)
            {
                metadata.Title = "Error extracting metadata";
                metadata.Description = $"Failed to extract metadata: {ex.Message}";
                metadata.Language = "en";
                metadata.RetrievedDate = DateTime.UtcNow;
            }

            return metadata;
        }

        private async Task<string> GetPageContentAsync(string url)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; VUniBox/1.0; +https://vunibox.com)");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch page content: {ex.Message}");
            }
        }

        private string? GetAttributeValue(HtmlNode node, string attributeName)
        {
            return node.GetAttributeValue(attributeName, null);
        }
    }
}