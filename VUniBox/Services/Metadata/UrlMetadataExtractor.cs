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
                // Clean and validate URL first
                url = CleanAndValidateUrl(url);
                
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
            catch (HttpRequestException ex) when (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            {
                Console.WriteLine($"[WARNING] Access denied for {url}, creating fallback metadata");
                return CreateFallbackMetadata(url, documentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to extract metadata from {url}: {ex.Message}");
                return CreateFallbackMetadata(url, documentType, ex.Message);
            }
        }

        /// <summary>
        /// Extracts metadata specifically from a research-oriented website.
        /// </summary>
        /// <param name="url">The URL of the research site.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata.</returns>
        public async Task<DocumentMetadataDto> ExtractFromResearchSiteAsync(string url)
        {
            try
            {
                // Clean and validate URL first
                url = CleanAndValidateUrl(url);
                
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
            catch (HttpRequestException ex) when (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            {
                Console.WriteLine($"[WARNING] Access denied for research site {url}, creating fallback metadata");
                return CreateFallbackMetadata(url, DocumentType.Research, ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to extract from research site {url}: {ex.Message}");
                return CreateFallbackMetadata(url, DocumentType.Research, ex.Message);
            }
        }

        /// <summary>
        /// Extracts metadata specifically from a book-related website.
        /// </summary>
        /// <param name="url">The URL of the book site.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata.</returns>
        public async Task<DocumentMetadataDto> ExtractFromBookSiteAsync(string url)
        {
            try
            {
                // Clean and validate URL first
                url = CleanAndValidateUrl(url);
                
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
            catch (HttpRequestException ex) when (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            {
                Console.WriteLine($"[WARNING] Access denied for book site {url}, creating fallback metadata");
                return CreateFallbackMetadata(url, DocumentType.Book, ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to extract from book site {url}: {ex.Message}");
                return CreateFallbackMetadata(url, DocumentType.Book, ex.Message);
            }
        }

        /// <summary>
        /// Extracts metadata specifically from a news-related website.
        /// </summary>
        /// <param name="url">The URL of the news site.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata.</returns>
        public async Task<DocumentMetadataDto> ExtractFromNewsSiteAsync(string url)
        {
            Console.WriteLine($"[DEBUG] ExtractFromNewsSiteAsync started for: {url}");
            
            try
            {
                // Clean and validate URL first
                url = CleanAndValidateUrl(url);
                Console.WriteLine($"[DEBUG] URL cleaned and validated: {url}");
                
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
            catch (HttpRequestException ex) when (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            {
                Console.WriteLine($"[WARNING] Access denied for news site {url}, creating fallback metadata");
                return CreateFallbackMetadata(url, DocumentType.Newspaper, ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to extract from news site {url}: {ex.Message}");
                return CreateFallbackMetadata(url, DocumentType.Newspaper, ex.Message);
            }
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
            catch (HttpRequestException ex) when (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            {
                Console.WriteLine($"[WARNING] Access denied (403) for {url}, creating fallback metadata");
                return CreateFallbackMetadata(url, DocumentType.Others, ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception in ExtractGenericMetadataAsync: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                
                // Return fallback metadata instead of error metadata
                return CreateFallbackMetadata(url, DocumentType.Others, ex.Message);
            }

            return metadata;
        }

        /// <summary>
        /// Creates fallback metadata when content cannot be extracted due to access restrictions.
        /// </summary>
        /// <param name="url">The URL that couldn't be accessed.</param>
        /// <param name="documentType">The classified document type.</param>
        /// <param name="errorMessage">Optional error message for debugging.</param>
        /// <returns>A fallback DocumentMetadataDto with basic information.</returns>
        private DocumentMetadataDto CreateFallbackMetadata(string url, DocumentType documentType, string errorMessage = null)
        {
            var uri = new Uri(url);
            var domain = uri.Host.ToLowerInvariant();
            
            // Extract title from URL structure
            var extractedTitle = ExtractTitleFromUrl(url, domain);
            
            // Determine source and publication type based on domain
            var (source, publicationType) = DetermineSourceAndType(domain, documentType);
            
            var metadata = new DocumentMetadataDto
            {
                URL = url,
                Title = extractedTitle ?? $"{publicationType} from {source}",
                Description = "Content could not be automatically extracted due to access restrictions. Please visit the original URL.",
                Abstract = "Content could not be automatically extracted due to access restrictions. Please visit the original URL.",
                Source = source,
                Language = "en",
                RetrievedDate = DateTime.UtcNow
            };
            
            // Add debug information if error message is provided
            if (!string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine($"[DEBUG] Fallback metadata created for {url}. Error: {errorMessage}");
            }
            
            return metadata;
        }

        /// <summary>
        /// Cleans and validates URL to fix common issues
        /// </summary>
        private string CleanAndValidateUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be null or empty");

            // Remove leading/trailing whitespace
            url = url.Trim();

            // Fix double protocols (hthttps:// -> https://, hhttp:// -> http://)
            if (url.StartsWith("hthttps://", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(1); // Remove the extra 'h'
                Console.WriteLine($"[DEBUG] Fixed malformed URL: removed extra 'h' from hthttps://");
            }
            else if (url.StartsWith("hhttp://", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(1); // Remove the extra 'h'
                Console.WriteLine($"[DEBUG] Fixed malformed URL: removed extra 'h' from hhttp://");
            }

            // Fix missing protocol
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
                Console.WriteLine($"[DEBUG] Added missing https:// protocol to URL");
            }

            // Validate that it's a proper URL
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? result) || 
                (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException($"Invalid URL format: {url}");
            }

            Console.WriteLine($"[DEBUG] URL validated and cleaned: {url}");
            return url;
        }

        /// <summary>
        /// Extracts title from URL structure for various academic and news sites.
        /// </summary>
        /// <param name="url">The URL to extract title from.</param>
        /// <param name="domain">The domain of the URL.</param>
        /// <returns>Extracted title or null if not possible.</returns>
        private string ExtractTitleFromUrl(string url, string domain)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;
                
                // ResearchGate: /publication/123456_Title_With_Underscores
                if (domain.Contains("researchgate"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(url, @"publication/\d+_(.+)");
                    if (match.Success)
                    {
                        var title = match.Groups[1].Value.Replace('_', ' ');
                        // Clean up common URL artifacts
                        title = System.Text.RegularExpressions.Regex.Replace(title, @"\?.*$", ""); // Remove query parameters
                        title = System.Text.RegularExpressions.Regex.Replace(title, @"#.*$", ""); // Remove fragments
                        title = System.Web.HttpUtility.UrlDecode(title);
                        
                        // Clean up common artifacts specific to ResearchGate
                        title = title.Replace("-", " ");
                        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+", " "); // Multiple spaces to single
                        title = title.Trim();
                        
                        return title;
                    }
                }
                
                // arXiv: /abs/2301.12345
                if (domain.Contains("arxiv"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(path, @"/abs/(\d+\.\d+)");
                    if (match.Success)
                    {
                        return $"arXiv Preprint {match.Groups[1].Value}";
                    }
                }
                
                // IEEE: extract from path
                if (domain.Contains("ieee"))
                {
                    var pathParts = path.Split('/').Where(p => !string.IsNullOrEmpty(p)).ToArray();
                    if (pathParts.Length > 0)
                    {
                        var lastPart = pathParts.Last().Replace('-', ' ').Replace('_', ' ');
                        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(lastPart);
                    }
                }
                
                // PubMed: extract PMC or PMID
                if (domain.Contains("pubmed") || domain.Contains("ncbi"))
                {
                    var pmcMatch = System.Text.RegularExpressions.Regex.Match(url, @"PMC(\d+)");
                    if (pmcMatch.Success)
                    {
                        return $"PubMed Article PMC{pmcMatch.Groups[1].Value}";
                    }
                    
                    var pmidMatch = System.Text.RegularExpressions.Regex.Match(url, @"pmid/(\d+)");
                    if (pmidMatch.Success)
                    {
                        return $"PubMed Article PMID:{pmidMatch.Groups[1].Value}";
                    }
                }
                
                // Generic URL parsing - use the last meaningful part
                var segments = path.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
                if (segments.Length > 0)
                {
                    var lastSegment = segments.Last();
                    
                    // Remove file extensions
                    lastSegment = System.Text.RegularExpressions.Regex.Replace(lastSegment, @"\.(html?|php|aspx?)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    // Replace separators with spaces
                    lastSegment = lastSegment.Replace('-', ' ').Replace('_', ' ').Replace('+', ' ');
                    
                    // Remove numbers that might be IDs
                    lastSegment = System.Text.RegularExpressions.Regex.Replace(lastSegment, @"^\d+\s*", "");
                    lastSegment = System.Text.RegularExpressions.Regex.Replace(lastSegment, @"\s*\d+$", "");
                    
                    if (lastSegment.Length > 5)
                    {
                        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(lastSegment.ToLower());
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Determines source name and publication type based on domain.
        /// </summary>
        /// <param name="domain">The domain to analyze.</param>
        /// <param name="documentType">The classified document type.</param>
        /// <returns>A tuple containing source name and publication type.</returns>
        private (string source, string publicationType) DetermineSourceAndType(string domain, DocumentType documentType)
        {
            // Academic/Research sites
            if (domain.Contains("researchgate")) return ("ResearchGate", "Academic Paper");
            if (domain.Contains("arxiv")) return ("arXiv", "Preprint");
            if (domain.Contains("ieee")) return ("IEEE Xplore", "Technical Paper");
            if (domain.Contains("pubmed") || domain.Contains("ncbi")) return ("PubMed", "Medical Research");
            if (domain.Contains("scholar.google")) return ("Google Scholar", "Academic Paper");
            if (domain.Contains("semanticscholar")) return ("Semantic Scholar", "Academic Paper");
            if (domain.Contains("acm.org")) return ("ACM Digital Library", "Technical Paper");
            if (domain.Contains("springer")) return ("Springer", "Academic Paper");
            if (domain.Contains("sciencedirect")) return ("ScienceDirect", "Academic Paper");
            if (domain.Contains("wiley")) return ("Wiley Online Library", "Academic Paper");
            
            // Vietnamese news sites
            if (domain.Contains("vnexpress")) return ("VnExpress", "News Article");
            if (domain.Contains("tuoitre")) return ("Tuổi Trẻ Online", "News Article");
            if (domain.Contains("thanhnien")) return ("Thanh Niên", "News Article");
            if (domain.Contains("dantri")) return ("Dân Trí", "News Article");
            if (domain.Contains("vietnamnet")) return ("VietNamNet", "News Article");
            
            // International news sites
            if (domain.Contains("bbc")) return ("BBC", "News Article");
            if (domain.Contains("cnn")) return ("CNN", "News Article");
            if (domain.Contains("reuters")) return ("Reuters", "News Article");
            if (domain.Contains("nytimes")) return ("The New York Times", "News Article");
            
            // Book sites
            if (domain.Contains("amazon")) return ("Amazon", "Book");
            if (domain.Contains("goodreads")) return ("Goodreads", "Book");
            if (domain.Contains("books.google")) return ("Google Books", "Book");
            
            // Fallback based on document type
            var source = domain.Replace("www.", "").Split('.')[0];
            source = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(source);
            
            var publicationType = documentType switch
            {
                DocumentType.Research => "Academic Paper",
                DocumentType.Book => "Book",
                DocumentType.Newspaper => "News Article",
                _ => "Web Document"
            };
            
            return (source, publicationType);
        }

        /// <summary>
        /// Try alternative URL strategies for blocked sites
        /// </summary>
        private async Task<string?> TryAlternativeUrlStrategies(string originalUrl)
        {
            var strategies = new List<string>();
            
            // ResearchGate alternatives
            if (originalUrl.Contains("researchgate.net"))
            {
                // Mobile version
                strategies.Add(originalUrl.Replace("www.researchgate.net", "m.researchgate.net"));
                
                // Extract publication ID and try direct PDF
                var pubMatch = System.Text.RegularExpressions.Regex.Match(originalUrl, @"publication/(\d+)");
                if (pubMatch.Success)
                {
                    var pubId = pubMatch.Groups[1].Value;
                    strategies.Add($"https://www.researchgate.net/publication/{pubId}.pdf");
                    strategies.Add($"https://www.researchgate.net/profile/publication/{pubId}");
                }
            }
            
            // arXiv alternatives
            if (originalUrl.Contains("arxiv.org"))
            {
                var arxivMatch = System.Text.RegularExpressions.Regex.Match(originalUrl, @"abs/(\d+\.\d+)");
                if (arxivMatch.Success)
                {
                    var arxivId = arxivMatch.Groups[1].Value;
                    strategies.Add($"https://arxiv.org/pdf/{arxivId}.pdf");
                    strategies.Add($"https://export.arxiv.org/abs/{arxivId}");
                }
            }
            
            // IEEE alternatives
            if (originalUrl.Contains("ieee.org"))
            {
                strategies.Add(originalUrl.Replace("ieeexplore.ieee.org", "www.computer.org"));
                strategies.Add(originalUrl + "?arnumber=");
            }
            
            foreach (var altUrl in strategies)
            {
                try
                {
                    Console.WriteLine($"[DEBUG] Trying alternative URL: {altUrl}");
                    
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(15);
                    
                    // Clear and set headers properly
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                    client.DefaultRequestHeaders.Add("Referer", "https://scholar.google.com/");
                    
                    var response = await client.GetAsync(altUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(content) && content.Contains("<title"))
                        {
                            Console.WriteLine($"[DEBUG] Alternative URL succeeded: {altUrl}");
                            return content;
                        }
                    }
                    
                    Console.WriteLine($"[DEBUG] Alternative URL {altUrl} returned: {response.StatusCode}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Alternative URL {altUrl} failed: {ex.Message}");
                }
                
                await Task.Delay(1000); // Small delay between attempts
            }
            
            return null;
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
                    
                    // Special handling for ResearchGate
                    if (url.Contains("researchgate.net"))
                    {
                        if (attempt == 0)
                        {
                            client.DefaultRequestHeaders.Add("Referer", "https://scholar.google.com/");
                            client.DefaultRequestHeaders.Add("Origin", "https://www.google.com");
                            // Add cookies to simulate real browsing session
                            client.DefaultRequestHeaders.Add("Cookie", "RG_locale=en; RG_analyticsOptOut=false");
                        }
                        
                        // Try ResearchGate mobile URL first (often less protected)
                        if (attempt == 0 && !url.Contains("m.researchgate"))
                        {
                            var mobileUrl = url.Replace("www.researchgate.net", "m.researchgate.net");
                            Console.WriteLine($"[DEBUG] Trying mobile ResearchGate URL: {mobileUrl}");
                            
                            try
                            {
                                var mobileResponse = await client.GetAsync(mobileUrl);
                                if (mobileResponse.IsSuccessStatusCode)
                                {
                                    var mobileContent = await mobileResponse.Content.ReadAsStringAsync();
                                    if (!string.IsNullOrEmpty(mobileContent) && mobileContent.Contains("<title"))
                                    {
                                        Console.WriteLine("[DEBUG] Mobile ResearchGate succeeded!");
                                        return mobileContent;
                                    }
                                }
                            }
                            catch (Exception mobileEx)
                            {
                                Console.WriteLine($"[DEBUG] Mobile ResearchGate failed: {mobileEx.Message}");
                            }
                        }
                    }
                    // Special handling for academic sites
                    else if (url.Contains("ieee.org") || url.Contains("acm.org") || url.Contains("springer.com"))
                    {
                        if (attempt == 0)
                        {
                            client.DefaultRequestHeaders.Add("Referer", "https://scholar.google.com/");
                            client.DefaultRequestHeaders.Add("Origin", "https://scholar.google.com");
                        }
                    }
                    // Add a referrer to make it look more natural for other sites
                    else if (attempt > 0)
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

            // If all attempts failed, try alternative URL strategies before giving up
            Console.WriteLine("[DEBUG] Trying alternative URL strategies...");
            var altContent = await TryAlternativeUrlStrategies(url);
            if (!string.IsNullOrEmpty(altContent))
            {
                return altContent;
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