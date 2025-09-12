using HtmlAgilityPack;
using VUniBox.Models.Enum;
using System.Text.RegularExpressions;
using VUniBox.Models.DTO;

namespace VUniBox.Services.Metadata
{
    /// <summary>
    /// Service for extracting metadata from URLs based on document type.
    /// </summary>
    public class UrlMetadataExtractor : IUrlMetadataExtractor
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="UrlMetadataExtractor"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for web requests.</param>
        public UrlMetadataExtractor(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Extracts metadata from a given URL based on the specified document type.
        /// </summary>
        /// <param name="url">The URL to extract metadata from.</param>
        /// <param name="documentType">The classified document type.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata.</returns>
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

        /// <summary>
        /// Extracts metadata specifically from a research-oriented website.
        /// </summary>
        /// <param name="url">The URL of the research site.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata.</returns>
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

        /// <summary>
        /// Extracts metadata specifically from a book-related website.
        /// </summary>
        /// <param name="url">The URL of the book site.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata.</returns>
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
                    "*[@class='publisher']",
                    "*[contains(@class, 'publisher')]",
                    "*[@class='imprint']",
                    "*[contains(@class, 'imprint')]"
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
                    "*[@class='author']",
                    "*[contains(@class, 'author')]",
                    "*[@class='authors']",
                    "*[contains(@class, 'authors')]"
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
                    "*[@class='publication-date']",
                    "*[contains(@class, 'publication-date')]",
                    "*[contains(@class, 'date')]"
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

        /// <summary>
        /// Extracts metadata specifically from a news-related website.
        /// </summary>
        /// <param name="url">The URL of the news site.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata.</returns>
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
                    "*[@class='publish-date']",
                    "*[contains(@class, 'publish-date')]",
                    "*[contains(@class, 'date')]",
                    "*[contains(@class, 'published')]"
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
                    "*[@class='author']",
                    "*[contains(@class, 'author')]",
                    "*[@class='byline']",
                    "*[contains(@class, 'byline')]"
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

        /// <summary>
        /// Extracts generic metadata from any given URL by parsing common HTML meta tags.
        /// </summary>
        /// <param name="url">The URL to extract metadata from.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted generic metadata.</returns>
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

        /// <summary>
        /// Retrieves the HTML content of a given URL.
        /// </summary>
        /// <param name="url">The URL to fetch content from.</param>
        /// <returns>The HTML content as a string.</returns>
        /// <exception cref="Exception">Thrown if fetching the page content fails.</exception>
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

        /// <summary>
        /// Gets the value of a specified HTML attribute from a node.
        /// </summary>
        /// <param name="node">The HTML node.</param>
        /// <param name="attributeName">The name of the attribute.</param>
        /// <returns>The attribute value, or null if the attribute is not found.</returns>
        private string? GetAttributeValue(HtmlNode node, string attributeName)
        {
            return node.GetAttributeValue(attributeName, null);
        }
    }
}