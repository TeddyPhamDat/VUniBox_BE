using HtmlAgilityPack;
using VUniBox.Models.Enum;
using System.Text.RegularExpressions;
using VUniBox.Models.DTO;
using System.Web;
using System.IO.Compression;

namespace VUniBox.Services.Metadata
{
    /// <summary>
    /// Vietnamese news site pattern extractor for fallback when scraping is blocked
    /// </summary>
    public static class VietnamNewsExtractor
    {
        // Map domain to site name for publisher field
        private static readonly Dictionary<string, string> NewsSiteNames = new Dictionary<string, string>
        {
            { "vnexpress.net", "VnExpress" },
            { "tuoitre.vn", "Tuổi Trẻ Online" },
            { "thanhnien.vn", "Thanh Niên" },
            { "dantri.com.vn", "Dân Trí" },
            { "vietnamnet.vn", "VietNamNet" },
            { "baomoi.com", "Báo Mới" },
            { "kenh14.vn", "Kênh 14" }
        };

        /// <summary>
        /// Extract article title from URL for Vietnamese news sites
        /// </summary>
        public static string ExtractTitleFromUrl(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                string domain = uri.Host.ToLower();
                
                // Extract the last part of the path
                string path = uri.AbsolutePath;
                
                // Handle VnExpress URLs (e.g., /tong-bi-thu-xu-ly-can-co-nhung-diem-nghen-ve-dat-dai-4940781.html)
                if (domain.Contains("vnexpress.net"))
                {
                    // Extract slug before the numeric ID
                    string slug = path;
                    if (path.Contains("-") && path.Contains(".html"))
                    {
                        // Remove leading slash and .html extension
                        slug = path.Substring(1).Replace(".html", "");
                        
                        // Find last numeric part and remove it
                        var parts = slug.Split('-');
                        if (parts.Length > 1 && Regex.IsMatch(parts[parts.Length - 1], @"^\d+$"))
                        {
                            slug = string.Join("-", parts.Take(parts.Length - 1));
                        }
                    }
                    
                    // Convert slug to title case with spaces
                    return ConvertSlugToTitle(slug);
                }
                
                // For other Vietnamese news sites
                var lastSegment = path.Split('/').LastOrDefault() ?? "";
                var withoutExtension = lastSegment.Replace(".html", "").Replace(".htm", "");
                
                // Extract the slug part (before any ID if exists)
                string titleSlug = withoutExtension;
                if (withoutExtension.Contains("-"))
                {
                    var parts = withoutExtension.Split('-');
                    // Check if the last part is numeric (ID)
                    if (parts.Length > 0 && Regex.IsMatch(parts[parts.Length - 1], @"^\d+$"))
                    {
                        titleSlug = string.Join("-", parts.Take(parts.Length - 1));
                    }
                }
                
                return ConvertSlugToTitle(titleSlug);
            }
            catch
            {
                // Return domain name if extraction fails
                try
                {
                    return new Uri(url).Host;
                }
                catch
                {
                    return "Bài báo";
                }
            }
        }
        
        /// <summary>
        /// Extract publisher name from URL
        /// </summary>
        public static string ExtractPublisherFromUrl(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                string domain = uri.Host.ToLower();
                
                foreach (var site in NewsSiteNames)
                {
                    if (domain.Contains(site.Key))
                    {
                        return site.Value;
                    }
                }
                
                return domain;
            }
            catch
            {
                return "Báo điện tử";
            }
        }
        
        /// <summary>
        /// Convert slug format to readable title
        /// </summary>
        private static string ConvertSlugToTitle(string slug)
        {
            // Replace hyphens with spaces
            string title = slug.Replace("-", " ");
            
            // Convert to title case
            title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(title);
            
            // Decode URL-encoded characters
            title = HttpUtility.UrlDecode(title);
            
            return title;
        }
    }

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
            
            // Configure HttpClient for automatic decompression if not already configured
            if (!_httpClient.DefaultRequestHeaders.Contains("Accept-Encoding"))
            {
                _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            }
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
            Console.WriteLine($"[DEBUG] ExtractFromNewsSiteAsync started for: {url}");
            
            var metadata = await ExtractGenericMetadataAsync(url);
            Console.WriteLine($"[DEBUG] ExtractGenericMetadataAsync completed. Title: {metadata.Title}");
            
            try
            {
                var html = await GetPageContentAsync(url);
                Console.WriteLine($"[DEBUG] GetPageContentAsync completed. HTML length: {html?.Length}");
                
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                Console.WriteLine($"[DEBUG] HTML document loaded successfully");

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
                            Console.WriteLine($"[DEBUG] Publication date extracted: {metadata.PublicationDate}");
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

            // Add fallback for Vietnamese news sites that block scraping
            if (metadata.Title == "Error extracting metadata" || string.IsNullOrEmpty(metadata.Title))
            {
                try
                {
                    Uri uri = new Uri(url);
                    string domain = uri.Host.ToLower();
                    
                    if (domain.Contains("vnexpress.net") || 
                        domain.Contains("tuoitre.vn") || 
                        domain.Contains("thanhnien.vn") || 
                        domain.Contains("dantri.com.vn") ||
                        domain.Contains("vietnamnet.vn") ||
                        domain.Contains("baomoi.com") ||
                        domain.Contains("kenh14.vn"))
                    {
                        Console.WriteLine("[DEBUG] Using Vietnamese news site pattern extractor for: " + url);
                        
                        metadata.Title = VietnamNewsExtractor.ExtractTitleFromUrl(url);
                        metadata.Publisher = VietnamNewsExtractor.ExtractPublisherFromUrl(url);
                        metadata.Source = metadata.Publisher;
                        metadata.RetrievedDate = DateTime.UtcNow;
                        metadata.Language = "vi";
                        
                        Console.WriteLine("[DEBUG] Extracted title from URL: " + metadata.Title);
                        Console.WriteLine("[DEBUG] Extracted publisher from URL: " + metadata.Publisher);
                        
                        // Clear the error description since we have valid data now
                        if (metadata.Description?.Contains("Error extracting news metadata") == true)
                        {
                            metadata.Description = "Metadata extracted from URL pattern (Vietnamese news site)";
                        }
                    }
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine("[DEBUG] Vietnamese news extractor fallback failed: " + fallbackEx.Message);
                }
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
            Console.WriteLine($"[DEBUG] ExtractGenericMetadataAsync started for: {url}");
            var metadata = new DocumentMetadataDto { URL = url };

            try
            {
                var html = await GetPageContentAsync(url);
                Console.WriteLine($"[DEBUG] GetPageContentAsync in ExtractGenericMetadataAsync completed. HTML length: {html?.Length}");
                
                if (string.IsNullOrEmpty(html))
                {
                    Console.WriteLine($"[ERROR] HTML content is null or empty");
                    metadata.Title = "Error: No content retrieved";
                    metadata.Description = "Failed to retrieve HTML content from URL";
                    return metadata;
                }
                
                // Show first 500 characters of HTML for debugging
                var htmlPreview = html.Length > 500 ? html.Substring(0, 500) + "..." : html;
                Console.WriteLine($"[DEBUG] HTML Preview: {htmlPreview}");
                
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                Console.WriteLine($"[DEBUG] HTML document loaded in ExtractGenericMetadataAsync");

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
                    Console.WriteLine($"[DEBUG] Trying title selector: {selector}");
                    var titleElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    Console.WriteLine($"[DEBUG] Title element found: {titleElement != null}");
                    
                    if (titleElement != null)
                    {
                        var contentAttr = GetAttributeValue(titleElement, "content");
                        var innerText = titleElement.InnerText?.Trim();
                        Console.WriteLine($"[DEBUG] Content attribute: '{contentAttr}', InnerText: '{innerText}'");
                        
                        var title = contentAttr ?? innerText;
                        Console.WriteLine($"[DEBUG] Final title candidate: '{title}', Length: {title?.Length}");
                        
                        if (!string.IsNullOrEmpty(title) && title.Length > 5)
                        {
                            metadata.Title = title;
                            Console.WriteLine($"[DEBUG] Title extracted successfully: {title}");
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] Title candidate rejected: empty or too short");
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(metadata.Title))
                {
                    Console.WriteLine($"[DEBUG] No title found, setting default");
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
                Console.WriteLine($"[ERROR] Exception in ExtractGenericMetadataAsync: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
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
            Exception? lastException = null;
            
            // Try different User-Agent strings to bypass restrictions
            var userAgents = new[]
            {
                // Latest Chrome on Windows
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36",
                // Latest Firefox on Windows  
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:130.0) Gecko/20100101 Firefox/130.0",
                // Latest Edge on Windows
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36 Edg/127.0.0.0",
                // Latest Safari on Mac
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.6 Safari/605.1.15"
            };

            // Different approaches for each attempt
            for (int attempt = 0; attempt < userAgents.Length; attempt++)
            {
                try
                {
                    // Create a new HttpClient for each attempt to avoid connection pooling issues
                    using var client = new HttpClient();
                    
                    // Set timeout
                    client.Timeout = TimeSpan.FromSeconds(30);
                    
                    // Clear default headers
                    client.DefaultRequestHeaders.Clear();
                    
                    // Set User-Agent
                    client.DefaultRequestHeaders.Add("User-Agent", userAgents[attempt]);
                    
                    // Set comprehensive headers that match real browsers exactly
                    if (attempt == 0) // Chrome-like headers
                    {
                        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                        client.DefaultRequestHeaders.Add("Accept-Language", "vi,en-US;q=0.9,en;q=0.8");
                        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br, zstd");
                        client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Chromium\";v=\"127\", \"Not;A=Brand\";v=\"99\"");
                        client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                        client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
                        client.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
                        client.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
                        client.DefaultRequestHeaders.Add("sec-fetch-site", "none");
                        client.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
                        client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");
                        client.DefaultRequestHeaders.Add("cache-control", "max-age=0");
                    }
                    else if (attempt == 1) // Firefox-like headers
                    {
                        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                        client.DefaultRequestHeaders.Add("Accept-Language", "vi,en-US;q=0.7,en;q=0.3");
                        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                        client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");
                        client.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
                        client.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
                        client.DefaultRequestHeaders.Add("sec-fetch-site", "none");
                        client.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
                    }
                    else if (attempt == 2) // Edge-like headers
                    {
                        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                        client.DefaultRequestHeaders.Add("Accept-Language", "vi,en;q=0.9");
                        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                        client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Microsoft Edge\";v=\"127\", \"Chromium\";v=\"127\", \"Not;A=Brand\";v=\"99\"");
                        client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                        client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
                        client.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
                        client.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
                        client.DefaultRequestHeaders.Add("sec-fetch-site", "none");
                        client.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
                        client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");
                    }
                    else // Safari-like headers
                    {
                        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                        client.DefaultRequestHeaders.Add("Accept-Language", "vi-vn");
                        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                        client.DefaultRequestHeaders.Add("cache-control", "max-age=0");
                    }
                    
                    // Add a referrer to make it look more natural
                    if (attempt > 0)
                    {
                        client.DefaultRequestHeaders.Add("Referer", "https://www.google.com/");
                    }

                    Console.WriteLine($"[DEBUG] Attempt {attempt + 1} with {userAgents[attempt].Split(' ')[0]} browser simulation");

                    var response = await client.GetAsync(url);
                    
                    Console.WriteLine($"[DEBUG] Response: {response.StatusCode} ({(int)response.StatusCode})");
                    
                    // Check if successful
                    if (response.IsSuccessStatusCode)
                    {
                        // Read content as bytes first to handle compression properly
                        var contentBytes = await response.Content.ReadAsByteArrayAsync();
                        Console.WriteLine($"[DEBUG] Response content length: {contentBytes?.Length}");
                        Console.WriteLine($"[DEBUG] Content encoding: {response.Content.Headers.ContentEncoding?.FirstOrDefault() ?? "none"}");
                        
                        string content;
                        
                        // Check content encoding and decompress if needed
                        var encoding = response.Content.Headers.ContentEncoding?.FirstOrDefault()?.ToLowerInvariant();
                        if (encoding == "gzip")
                        {
                            using var gzipStream = new System.IO.Compression.GZipStream(new MemoryStream(contentBytes), System.IO.Compression.CompressionMode.Decompress);
                            using var reader = new StreamReader(gzipStream, System.Text.Encoding.UTF8);
                            content = await reader.ReadToEndAsync();
                            Console.WriteLine($"[DEBUG] Decompressed GZIP content length: {content?.Length}");
                        }
                        else if (encoding == "deflate")
                        {
                            using var deflateStream = new System.IO.Compression.DeflateStream(new MemoryStream(contentBytes), System.IO.Compression.CompressionMode.Decompress);
                            using var reader = new StreamReader(deflateStream, System.Text.Encoding.UTF8);
                            content = await reader.ReadToEndAsync();
                            Console.WriteLine($"[DEBUG] Decompressed DEFLATE content length: {content?.Length}");
                        }
                        else if (encoding == "br")
                        {
                            using var brotliStream = new System.IO.Compression.BrotliStream(new MemoryStream(contentBytes), System.IO.Compression.CompressionMode.Decompress);
                            using var reader = new StreamReader(brotliStream, System.Text.Encoding.UTF8);
                            content = await reader.ReadToEndAsync();
                            Console.WriteLine($"[DEBUG] Decompressed BROTLI content length: {content?.Length}");
                        }
                        else
                        {
                            // Try as regular string first
                            content = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"[DEBUG] Plain text content length: {content?.Length}");
                        }
                        
                        Console.WriteLine($"[DEBUG] Final content starts with: {content?.Substring(0, Math.Min(200, content?.Length ?? 0))}");
                        
                        // Verify we got actual HTML content
                        if (!string.IsNullOrEmpty(content) && 
                            (content.Contains("<html", StringComparison.OrdinalIgnoreCase) || 
                             content.Contains("<!doctype", StringComparison.OrdinalIgnoreCase) ||
                             content.Contains("<head", StringComparison.OrdinalIgnoreCase) ||
                             content.Contains("<body", StringComparison.OrdinalIgnoreCase) ||
                             content.Contains("<title", StringComparison.OrdinalIgnoreCase)))
                        {
                            Console.WriteLine($"[DEBUG] HTML validation passed for attempt {attempt + 1}");
                            return content;
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] HTML validation failed for attempt {attempt + 1}. Content does not appear to be valid HTML");
                        }
                    }
                    
                    lastException = new Exception($"HTTP {(int)response.StatusCode} {response.StatusCode}: {response.ReasonPhrase}");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Console.WriteLine($"[DEBUG] Attempt {attempt + 1} failed: {ex.Message}");
                }

                // Wait between attempts with longer delays
                if (attempt < userAgents.Length - 1)
                {
                    await Task.Delay(2000 + (attempt * 1000)); // 2-5 second delays
                }
            }

            // If all attempts failed, try one more time with minimal headers
            try
            {
                Console.WriteLine("[DEBUG] Trying fallback method with minimal headers...");
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Clear();
                
                // Only essential headers to avoid detection
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                client.DefaultRequestHeaders.Add("Accept", "*/*");
                client.DefaultRequestHeaders.Add("Accept-Language", "vi");
                
                var response = await client.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(content) && content.Contains("<title", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("[DEBUG] Fallback method succeeded!");
                        return content;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Fallback method also failed: {ex.Message}");
            }

            // If all attempts failed, throw the last exception
            throw new Exception($"Failed to fetch page content after {userAgents.Length} attempts. Last error: {lastException?.Message}");
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