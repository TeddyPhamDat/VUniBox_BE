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
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="UrlMetadataExtractor"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for web requests.</param>
        /// <param name="configuration">The configuration to get ScraperAPI settings.</param>
        public UrlMetadataExtractor(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            
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
                
                // Use specialized extractors for known academic sites directly to avoid duplicate processing
                if (IsResearchGateUrl(url))
                {
                    var html = await GetPageContentAsync(url);
                    return ExtractResearchGateMetadata(html, url);
                }
                else if (IsArxivUrl(url))
                {
                    var html = await GetPageContentAsync(url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    return ExtractArxivMetadata(doc, url);
                }
                else if (IsPubMedUrl(url))
                {
                    var html = await GetPageContentAsync(url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    return ExtractPubMedMetadata(doc, url);
                }
                else if (IsIEEEUrl(url))
                {
                    var html = await GetPageContentAsync(url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    return ExtractIEEEMetadata(doc, url);
                }
                else if (IsSpringerUrl(url))
                {
                    var html = await GetPageContentAsync(url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    return ExtractSpringerMetadata(doc, url);
                }
                else if (IsGoogleScholarUrl(url))
                {
                    var html = await GetPageContentAsync(url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    return ExtractGoogleScholarMetadata(doc, url);
                }
                else if (IsJSTORUrl(url))
                {
                    var html = await GetPageContentAsync(url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    return ExtractJSTORMetadata(doc, url);
                }
                else if (IsScienceDirectUrl(url))
                {
                    var html = await GetPageContentAsync(url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    return ExtractScienceDirectMetadata(doc, url);
                }
                
                // Fall back to generic extraction for non-specialized research sites
                return await ExtractGenericMetadataAsync(url);
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
        /// Extracts detailed metadata specifically from ResearchGate publications.
        /// </summary>
        /// <param name="html">The HTML content from ResearchGate.</param>
        /// <param name="url">The original URL.</param>
        /// <returns>Enhanced metadata with ResearchGate-specific information.</returns>
        private DocumentMetadataDto ExtractResearchGateMetadata(string html, string url)
        {
            var metadata = new DocumentMetadataDto { URL = url, Source = "ResearchGate" };
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            try
            {
                Console.WriteLine("[DEBUG] Starting ResearchGate-specific metadata extraction");
                Console.WriteLine($"[DEBUG] HTML content length: {html?.Length ?? 0}");
                
                // Debug: Print first 2000 characters of HTML to see structure
                var htmlPreview = html?.Length > 2000 
                    ? html.Substring(0, 2000) 
                    : html ?? "";
                Console.WriteLine($"[DEBUG] HTML Preview (first 2000 chars):\n{htmlPreview}");
                Console.WriteLine("[DEBUG] ===== END HTML PREVIEW =====");
                
                // Debug: Check for citation meta tags
                var allMetaTags = doc.DocumentNode.SelectNodes("//meta[@name[starts-with(., 'citation_')]]");
                Console.WriteLine($"[DEBUG] Found {allMetaTags?.Count ?? 0} citation meta tags in HTML");
                if (allMetaTags != null)
                {
                    foreach (var meta in allMetaTags.Take(5)) // Show first 5 to avoid spam
                    {
                        var name = GetAttributeValue(meta, "name");
                        var content = GetAttributeValue(meta, "content");
                        Console.WriteLine($"[DEBUG] Meta: {name} = '{content?.Substring(0, Math.Min(100, content?.Length ?? 0))}...'");
                    }
                }
                
                // Debug: Check ALL meta tags to see what we have
                var allMetas = doc.DocumentNode.SelectNodes("//meta");
                Console.WriteLine($"[DEBUG] Total meta tags found: {allMetas?.Count ?? 0}");
                if (allMetas != null && allMetas.Count > 0)
                {
                    Console.WriteLine("[DEBUG] First 15 meta tags:");
                    for (int i = 0; i < Math.Min(15, allMetas.Count); i++)
                    {
                        var meta = allMetas[i];
                        var name = meta.GetAttributeValue("name", "");
                        var property = meta.GetAttributeValue("property", "");
                        var content = meta.GetAttributeValue("content", "");
                        Console.WriteLine($"[DEBUG] Meta {i}: name='{name}' property='{property}' content='{content?.Substring(0, Math.Min(150, content?.Length ?? 0))}...'");
                    }
                    
                    // Look specifically for OpenGraph and Twitter meta tags
                    Console.WriteLine("[DEBUG] Looking for OpenGraph and Twitter meta tags:");
                    var ogMetas = doc.DocumentNode.SelectNodes("//meta[@property[starts-with(., 'og:')]]");
                    if (ogMetas != null)
                    {
                        foreach (var meta in ogMetas)
                        {
                            var property = meta.GetAttributeValue("property", "");
                            var content = meta.GetAttributeValue("content", "");
                            Console.WriteLine($"[DEBUG] OG: {property} = '{content}'");
                        }
                    }
                    
                    var twitterMetas = doc.DocumentNode.SelectNodes("//meta[@property[starts-with(., 'twitter:')]]");
                    if (twitterMetas != null)
                    {
                        foreach (var meta in twitterMetas)
                        {
                            var property = meta.GetAttributeValue("property", "");
                            var content = meta.GetAttributeValue("content", "");
                            Console.WriteLine($"[DEBUG] Twitter: {property} = '{content}'");
                        }
                    }
                }

                // Extract title - prioritize OpenGraph which seems to be available
                var titleMeta = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']") ??
                               doc.DocumentNode.SelectSingleNode("//meta[@name='citation_title']");
                if (titleMeta != null)
                {
                    metadata.Title = GetAttributeValue(titleMeta, "content");
                    Console.WriteLine($"[DEBUG] ResearchGate title extracted via {(titleMeta.GetAttributeValue("property", "") == "og:title" ? "OpenGraph" : "citation meta")}: {metadata.Title}");
                }

                // If no title from meta tags, try selectors
                if (string.IsNullOrEmpty(metadata.Title))
                {
                    var titleSelectors = new[]
                    {
                        "//h1[@class='research-detail-header-section__title']",
                        "//h1[@class='publication-detail-header__title']", 
                        "//h1[contains(@class, 'publication-title')]",
                        "//h1",
                        "//title"
                    };

                    foreach (var selector in titleSelectors)
                    {
                        var titleElement = doc.DocumentNode.SelectSingleNode(selector);
                        if (titleElement != null)
                        {
                            var title = titleElement.InnerText?.Trim();
                            
                            if (!string.IsNullOrEmpty(title) && title.Length > 5)
                            {
                                metadata.Title = title;
                                Console.WriteLine($"[DEBUG] ResearchGate title extracted via {selector}: {title}");
                                break;
                            }
                        }
                    }
                }

                // Extract authors via citation meta tags first
                var authorMetas = doc.DocumentNode.SelectNodes("//meta[@name='citation_author']");
                Console.WriteLine($"[DEBUG] Found {authorMetas?.Count ?? 0} citation_author meta tags");
                if (authorMetas != null && authorMetas.Any())
                {
                    var authors = authorMetas.Select(meta => GetAttributeValue(meta, "content"))
                                           .Where(author => !string.IsNullOrEmpty(author))
                                           .ToList();
                    if (authors.Any())
                    {
                        metadata.Authors = string.Join(", ", authors);
                        metadata.Author = authors.First();
                        Console.WriteLine($"[DEBUG] ResearchGate authors via citation meta: {metadata.Authors}");
                    }
                }

                // If no citation authors, try ResearchGate specific selectors
                if (string.IsNullOrEmpty(metadata.Authors))
                {
                    Console.WriteLine("[DEBUG] No citation authors found, trying ResearchGate DOM selectors");
                    var authorSelectors = new[]
                    {
                        // Modern ResearchGate author selectors (2024+)
                        "//div[contains(@class, 'authors')]//a[contains(@class, 'author')]",
                        "//div[@class='authors']//a",
                        "//span[contains(@class, 'author-name')]",
                        "//a[contains(@class, 'author-name')]",
                        "//div[contains(@class, 'nova-legacy-v-person-list__item')]//span[@class='nova-legacy-v-person-item__title']",
                        "//div[contains(@class, 'publication-header-authors')]//a[contains(@class, 'nova-legacy-v-person-item__title')]",
                        // Fallback to any links that might be authors
                        "//a[contains(@href, '/profile/')]"
                    };

                    var authors = new List<string>();
                    foreach (var selector in authorSelectors)
                    {
                        var authorElements = doc.DocumentNode.SelectNodes(selector);
                        Console.WriteLine($"[DEBUG] Selector '{selector}' found {authorElements?.Count ?? 0} elements");
                        if (authorElements != null)
                        {
                            foreach (var element in authorElements)
                            {
                                var author = element.InnerText?.Trim();
                                if (!string.IsNullOrEmpty(author) && author.Length > 2 && author.Length < 100)
                                {
                                    // Basic filtering to avoid junk
                                    if (!author.Contains("@") && !author.Contains("http") && 
                                        !author.ToLower().Contains("researchgate") &&
                                        !authors.Contains(author, StringComparer.OrdinalIgnoreCase))
                                    {
                                        authors.Add(author);
                                        Console.WriteLine($"[DEBUG] Found author: '{author}'");
                                    }
                                }
                            }
                            if (authors.Any()) break; // Stop after first successful selector
                        }
                    }

                    if (authors.Any())
                    {
                        metadata.Authors = string.Join(", ", authors);
                        metadata.Author = authors.First();
                        Console.WriteLine($"[DEBUG] ResearchGate authors via DOM: {metadata.Authors}");
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] No authors found via DOM selectors either");
                    }
                }

                // Extract abstract - try multiple approaches prioritizing OpenGraph
                Console.WriteLine("[DEBUG] Extracting abstract...");
                var abstractSelectors = new[]
                {
                    // Start with OpenGraph description which is often available
                    "//meta[@property='og:description']",
                    "//meta[@property='twitter:description']",
                    "//meta[@name='description']",
                    // Then try citation abstract
                    "//meta[@name='citation_abstract']",
                    // Then try DOM selectors for abstract content
                    "//div[contains(@class, 'research-detail-middle-section__abstract')]//div[contains(@class, 'nova-legacy-e-text')]",
                    "//div[contains(@class, 'publication-abstract')]//div[contains(@class, 'nova-legacy-e-text')]",
                    "//div[@class='abstract-content']",
                    "//div[contains(@class, 'abstract')]",
                    "//section[contains(@class, 'abstract')]//p"
                };

                foreach (var selector in abstractSelectors)
                {
                    var abstractElement = doc.DocumentNode.SelectSingleNode(selector);
                    Console.WriteLine($"[DEBUG] Abstract selector '{selector}' found: {abstractElement != null}");
                    if (abstractElement != null)
                    {
                        var abstractText = abstractElement.Name == "meta" 
                            ? GetAttributeValue(abstractElement, "content")
                            : abstractElement.InnerText?.Trim();
                        
                        if (!string.IsNullOrEmpty(abstractText) && abstractText.Length > 30)
                        {
                            // Clean up common ResearchGate suffixes
                            abstractText = abstractText.Replace(" | Find, read and cite all the research you need on ResearchGate", "");
                            abstractText = abstractText.Replace("Find, read and cite all the research you need on ResearchGate", "");
                            
                            metadata.Abstract = abstractText;
                            metadata.Description = abstractText;
                            Console.WriteLine($"[DEBUG] ResearchGate abstract found via {selector}: {abstractText.Substring(0, Math.Min(150, abstractText.Length))}...");
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] Abstract found but too short: '{abstractText}' (length: {abstractText?.Length ?? 0})");
                        }
                    }
                }

                // Extract DOI
                Console.WriteLine("[DEBUG] Extracting DOI...");
                var doiMeta = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_doi']");
                Console.WriteLine($"[DEBUG] DOI meta tag found: {doiMeta != null}");
                if (doiMeta != null)
                {
                    metadata.DOI = GetAttributeValue(doiMeta, "content");
                    Console.WriteLine($"[DEBUG] ResearchGate DOI via meta: {metadata.DOI}");
                }

                // Extract journal/conference
                Console.WriteLine("[DEBUG] Extracting journal/conference...");
                var journalMeta = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_journal_title']") ??
                                 doc.DocumentNode.SelectSingleNode("//meta[@name='citation_conference_title']");
                Console.WriteLine($"[DEBUG] Journal meta tag found: {journalMeta != null}");
                if (journalMeta != null)
                {
                    metadata.Journal = GetAttributeValue(journalMeta, "content");
                    Console.WriteLine($"[DEBUG] ResearchGate journal via meta: {metadata.Journal}");
                }

                // Extract publication date
                Console.WriteLine("[DEBUG] Extracting publication date...");
                var dateMeta = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_publication_date']");
                Console.WriteLine($"[DEBUG] Date meta tag found: {dateMeta != null}");
                if (dateMeta != null)
                {
                    var dateStr = GetAttributeValue(dateMeta, "content");
                    Console.WriteLine($"[DEBUG] Date string: '{dateStr}'");
                    if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var pubDate))
                    {
                        metadata.PublicationDate = DateOnly.FromDateTime(pubDate);
                        Console.WriteLine($"[DEBUG] ResearchGate date: {dateStr}");
                    }
                }

                metadata.Language = "en"; // Most ResearchGate papers are in English
                metadata.RetrievedDate = DateTime.UtcNow;
                
                // Final cleanup and validation
                if (string.IsNullOrEmpty(metadata.Authors))
                {
                    Console.WriteLine("[DEBUG] No authors found, marking as unknown...");
                    metadata.Authors = "Authors not available";
                    metadata.Author = "Unknown";
                }
                
                // Clean up title if it contains PDF prefix
                if (!string.IsNullOrEmpty(metadata.Title))
                {
                    metadata.Title = metadata.Title.Replace("(PDF) ", "").Trim();
                }
                
                // If we have at least a title, this is a success
                if (!string.IsNullOrEmpty(metadata.Title))
                {
                    Console.WriteLine("[DEBUG] ResearchGate metadata extraction completed successfully");
                    return metadata;
                }
                else
                {
                    Console.WriteLine("[DEBUG] ResearchGate metadata extraction failed - no title found");
                    // Try to extract title from URL as fallback
                    var titleFromUrl = ExtractTitleFromUrl(url, "researchgate.net");
                    if (!string.IsNullOrEmpty(titleFromUrl))
                    {
                        metadata.Title = titleFromUrl;
                        metadata.Description = "Metadata được trích xuất một phần từ URL do hạn chế truy cập.";
                    }
                    else
                    {
                        metadata.Title = "Tài liệu từ ResearchGate";
                        metadata.Description = "Không thể trích xuất metadata từ ResearchGate. Vui lòng truy cập URL gốc.";
                    }
                }
                
                return metadata;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ResearchGate metadata extraction failed: {ex.Message}");
                metadata.Title = ExtractTitleFromUrl(url, "researchgate.net") ?? "Tài liệu từ ResearchGate";
                metadata.Description = $"Lỗi trích xuất ResearchGate metadata: {ex.Message}";
                return metadata;
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
                        metadata.Language = "vi";
                    }
                    metadata.RetrievedDate = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    metadata.Description = $"Lỗi trích xuất metadata sách: {ex.Message}";
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
                        metadata.Language = "vi";
                    }
                    metadata.RetrievedDate = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    metadata.Description = $"Lỗi trích xuất metadata tin tức: {ex.Message}";
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
                            if (metadata.Description?.Contains("Lỗi trích xuất metadata tin tức") == true)
                            {
                                metadata.Description = "Metadata được trích xuất từ cấu trúc URL (trang tin tức Việt Nam)";
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
            var metadata = new DocumentMetadataDto { URL = url };

            try
            {
                Console.WriteLine($"[DEBUG] ExtractGenericMetadataAsync called for: {url}");
                var html = await GetPageContentAsync(url);
                
                if (string.IsNullOrEmpty(html))
                {
                    metadata.Title = "Lỗi: Không thể truy cập nội dung";
                    metadata.Description = "Không thể truy xuất nội dung HTML từ URL";
                    return metadata;
                }
                
                Console.WriteLine($"[DEBUG] HTML retrieved for generic extraction, length: {html.Length}");
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Extract standard web metadata for non-specialized sites
                return ExtractStandardWebMetadata(doc, url);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            {
                return CreateFallbackMetadata(url, DocumentType.Others, ex.Message);
            }
            catch (Exception ex)
            {
                return CreateFallbackMetadata(url, DocumentType.Others, ex.Message);
            }
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
            
            // Create enhanced metadata based on the specific academic site
            var metadata = new DocumentMetadataDto
            {
                URL = url,
                Source = source,
                Language = "en", // Most academic papers are in English
                RetrievedDate = DateTime.UtcNow
            };
            
            // Customize based on the specific academic site
            if (domain.Contains("researchgate.net"))
            {
                metadata.Title = extractedTitle ?? "ResearchGate Publication";
                metadata.Description = "This publication is available on ResearchGate. Due to access restrictions, automatic metadata extraction was not possible. The full text and metadata can be accessed directly on ResearchGate.";
                metadata.Abstract = "Abstract and full content available on ResearchGate platform. Please visit the original URL for complete access to the publication.";
                metadata.Publisher = "ResearchGate";
                
                // Try to extract more info from URL structure
                var pubMatch = System.Text.RegularExpressions.Regex.Match(url, @"publication/(\d+)_(.+)");
                if (pubMatch.Success)
                {
                    var urlTitle = pubMatch.Groups[2].Value.Replace("_", " ");
                    urlTitle = System.Net.WebUtility.UrlDecode(urlTitle);
                    metadata.Title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(urlTitle.ToLower());
                }
            }
            else if (domain.Contains("arxiv.org"))
            {
                metadata.Title = extractedTitle ?? "arXiv Preprint";
                metadata.Description = "This is an arXiv preprint paper. Due to access restrictions, automatic metadata extraction was not possible. The full text is available on arXiv.";
                metadata.Abstract = "Preprint abstract available on arXiv. Please visit the original URL for complete access to the paper.";
                metadata.Publisher = "arXiv";
                
                // Extract arXiv ID and create DOI
                var arxivMatch = System.Text.RegularExpressions.Regex.Match(url, @"abs/(\d+\.\d+)");
                if (arxivMatch.Success)
                {
                    var arxivId = arxivMatch.Groups[1].Value;
                    metadata.DOI = $"arXiv:{arxivId}";
                    metadata.Title = $"arXiv:{arxivId} - {extractedTitle ?? "Preprint"}";
                }
            }
            else if (domain.Contains("ieee.org"))
            {
                metadata.Title = extractedTitle ?? "IEEE Publication";
                metadata.Description = "This is an IEEE publication. Due to access restrictions, automatic metadata extraction was not possible. The full text is available through IEEE Xplore.";
                metadata.Abstract = "Abstract available on IEEE Xplore. Please visit the original URL for complete access to the publication.";
                metadata.Publisher = "IEEE";
                metadata.Journal = "IEEE Conference/Journal";
            }
            else if (domain.Contains("springer.com"))
            {
                metadata.Title = extractedTitle ?? "Springer Publication";
                metadata.Description = "This is a Springer publication. Due to access restrictions, automatic metadata extraction was not possible. The full text is available through Springer Link.";
                metadata.Abstract = "Abstract available on Springer Link. Please visit the original URL for complete access to the publication.";
                metadata.Publisher = "Springer";
            }
            else if (domain.Contains("acm.org"))
            {
                metadata.Title = extractedTitle ?? "ACM Publication";
                metadata.Description = "This is an ACM publication. Due to access restrictions, automatic metadata extraction was not possible. The full text is available through ACM Digital Library.";
                metadata.Abstract = "Abstract available on ACM Digital Library. Please visit the original URL for complete access to the publication.";
                metadata.Publisher = "ACM";
            }
            else if (domain.Contains("pubmed") || domain.Contains("ncbi.nlm.nih.gov"))
            {
                metadata.Title = extractedTitle ?? "PubMed Publication";
                metadata.Description = "This is a medical/biological publication from PubMed. Due to access restrictions, automatic metadata extraction was not possible.";
                metadata.Abstract = "Medical abstract available on PubMed. Please visit the original URL for complete access to the publication.";
                metadata.Publisher = "PubMed/NCBI";
                metadata.Subject = "Medicine/Biology";
            }
            else
            {
                // Generic academic publication fallback
                metadata.Title = extractedTitle ?? $"{publicationType} từ {source}";
                metadata.Description = "Không thể trích xuất nội dung tự động do hạn chế truy cập. Vui lòng truy cập URL gốc để xem đầy đủ nội dung.";
                metadata.Abstract = "Tóm tắt và nội dung đầy đủ có sẵn trên trang gốc. Vui lòng truy cập URL để xem chi tiết.";
                metadata.Language = "vi";
            }
            
            // Set default author info for academic sources
            if (documentType == DocumentType.Research)
            {
                metadata.Authors = "Authors available on original publication page";
                metadata.Author = "Unknown";
            }
            
            // Add debug information if error message is provided
            if (!string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine($"[DEBUG] Enhanced fallback metadata created for {url}. Error: {errorMessage}");
                Console.WriteLine($"[DEBUG] Title: {metadata.Title}");
                Console.WriteLine($"[DEBUG] Source: {metadata.Source}");
                Console.WriteLine($"[DEBUG] Publisher: {metadata.Publisher}");
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
                        return $"Bài nghiên cứu arXiv {match.Groups[1].Value}";
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
                        return $"Bài báo PubMed PMC{pmcMatch.Groups[1].Value}";
                    }
                    
                    var pmidMatch = System.Text.RegularExpressions.Regex.Match(url, @"pmid/(\d+)");
                    if (pmidMatch.Success)
                    {
                        return $"Bài báo PubMed PMID:{pmidMatch.Groups[1].Value}";
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
        if (domain.Contains("researchgate")) return ("ResearchGate", "Bài nghiên cứu khoa học");
        if (domain.Contains("arxiv")) return ("arXiv", "Bài nghiên cứu sơ bộ");
        if (domain.Contains("ieee")) return ("IEEE Xplore", "Bài báo kỹ thuật");
        if (domain.Contains("pubmed") || domain.Contains("ncbi")) return ("PubMed", "Nghiên cứu y học");
        if (domain.Contains("scholar.google")) return ("Google Scholar", "Bài nghiên cứu khoa học");
        if (domain.Contains("semanticscholar")) return ("Semantic Scholar", "Bài nghiên cứu khoa học");
        if (domain.Contains("acm.org")) return ("ACM Digital Library", "Bài báo kỹ thuật");
        if (domain.Contains("springer")) return ("Springer", "Bài nghiên cứu khoa học");
        if (domain.Contains("sciencedirect")) return ("ScienceDirect", "Bài nghiên cứu khoa học");
        if (domain.Contains("wiley")) return ("Wiley Online Library", "Bài nghiên cứu khoa học");
        
        // Vietnamese news sites
        if (domain.Contains("vnexpress")) return ("VnExpress", "Bài báo");
        if (domain.Contains("tuoitre")) return ("Tuổi Trẻ Online", "Bài báo");
        if (domain.Contains("thanhnien")) return ("Thanh Niên", "Bài báo");
        if (domain.Contains("dantri")) return ("Dân Trí", "Bài báo");
        if (domain.Contains("vietnamnet")) return ("VietNamNet", "Bài báo");
            
        // International news sites
        if (domain.Contains("bbc")) return ("BBC", "Bài báo");
        if (domain.Contains("cnn")) return ("CNN", "Bài báo");
        if (domain.Contains("reuters")) return ("Reuters", "Bài báo");
        if (domain.Contains("nytimes")) return ("The New York Times", "Bài báo");
            
        // Book sites
        if (domain.Contains("amazon")) return ("Amazon", "Sách");
        if (domain.Contains("goodreads")) return ("Goodreads", "Sách");
        if (domain.Contains("books.google")) return ("Google Books", "Sách");
            
            // Fallback based on document type
            var source = domain.Replace("www.", "").Split('.')[0];
            source = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(source);
            
            var publicationType = documentType switch
            {
                DocumentType.Research => "Bài nghiên cứu khoa học",
                DocumentType.Book => "Sách",
                DocumentType.Newspaper => "Bài báo",
                _ => "Tài liệu web"
            };
            
            return (source, publicationType);
        }

        /// <summary>
        /// Try proxy strategies for difficult sites
        /// </summary>
        private async Task TryProxyStrategies(string url)
        {
            // This is a placeholder for future proxy implementation
            // For now, we'll add additional delay and different request patterns
            Console.WriteLine("[DEBUG] Implementing additional access strategies...");
            
            try
            {
                // Strategy: Try with longer delays between requests
                await Task.Delay(3000);
                
                // Strategy: Try with different request patterns
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(45);
                client.DefaultRequestHeaders.Clear();
                
                // Simulate real browsing behavior
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
                client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
                client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
                client.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
                client.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
                client.DefaultRequestHeaders.Add("sec-fetch-site", "none");
                client.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
                client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");
                
                // Add site-specific headers
                if (url.Contains("researchgate.net"))
                {
                    client.DefaultRequestHeaders.Add("Referer", "https://scholar.google.com/");
                    // Simulate being referred from Google Scholar
                    client.DefaultRequestHeaders.Add("Origin", "https://scholar.google.com");
                }
                
                Console.WriteLine("[DEBUG] Trying enhanced browser simulation...");
                var response = await client.GetAsync(url);
                Console.WriteLine($"[DEBUG] Enhanced simulation response: {response.StatusCode}");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Proxy strategies failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Try advanced browser simulation techniques
        /// </summary>
        private async Task<string?> TryAdvancedBrowserSimulation(string url)
        {
            Console.WriteLine("[DEBUG] Trying advanced browser simulation techniques...");
            
            var advancedUserAgents = new[]
            {
                // Latest Chrome
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                // Latest Firefox  
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0",
                // Latest Edge
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0",
                // Safari on macOS
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Version/17.0 Safari/537.36",
                // Academic crawler simulation
                "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)",
                // Research bot simulation
                "Mozilla/5.0 (compatible; ResearchBot/1.0; +https://example.com/bot)",
            };
            
            for (int i = 0; i < advancedUserAgents.Length; i++)
            {
                try
                {
                    Console.WriteLine($"[DEBUG] Advanced simulation attempt {i + 1}/{advancedUserAgents.Length}");
                    
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(60);
                    client.DefaultRequestHeaders.Clear();
                    
                    client.DefaultRequestHeaders.Add("User-Agent", advancedUserAgents[i]);
                    
                    // Add different headers based on user agent
                    if (advancedUserAgents[i].Contains("Chrome"))
                    {
                        client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
                        client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                        client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
                    }
                    else if (advancedUserAgents[i].Contains("Firefox"))
                    {
                        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
                    }
                    else if (advancedUserAgents[i].Contains("Googlebot"))
                    {
                        // Googlebot specific headers
                        client.DefaultRequestHeaders.Add("Accept", "*/*");
                        client.DefaultRequestHeaders.Add("Accept-Language", "en");
                    }
                    
                    // Site-specific optimizations
                    if (url.Contains("researchgate.net"))
                    {
                        // Add ResearchGate specific headers
                        client.DefaultRequestHeaders.Add("Referer", "https://www.google.com/search?q=researchgate");
                        client.DefaultRequestHeaders.Add("sec-fetch-site", "cross-site");
                        client.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
                        client.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
                        
                        // Add cookies that might help
                        client.DefaultRequestHeaders.Add("Cookie", "RG_locale=en; RG_analyticsOptOut=false; consent_status=accepted");
                    }
                    
                    var response = await client.GetAsync(url);
                    Console.WriteLine($"[DEBUG] Advanced simulation {i + 1} response: {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(content) && 
                            (content.Contains("<title", StringComparison.OrdinalIgnoreCase) ||
                             content.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
                             content.Contains("<head", StringComparison.OrdinalIgnoreCase)))
                        {
                            Console.WriteLine($"[DEBUG] Advanced simulation {i + 1} succeeded!");
                            return content;
                        }
                    }
                    
                    // Wait between attempts to avoid rate limiting
                    if (i < advancedUserAgents.Length - 1)
                    {
                        await Task.Delay(2000);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Advanced simulation {i + 1} failed: {ex.Message}");
                }
            }
            
            return null;
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
        /// Retrieves HTML content using ScraperAPI when direct access fails.
        /// </summary>
        /// <param name="url">The URL to fetch content from.</param>
        /// <returns>The HTML content as a string.</returns>
        private async Task<string> GetPageContentWithScraperAPI(string url)
        {
            try
            {
                var apiKey = _configuration["ScraperAPI:ApiKey"];
                var baseUrl = _configuration["ScraperAPI:BaseUrl"];
                
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new Exception("ScraperAPI key not configured");
                }

                var encodedUrl = Uri.EscapeDataString(url);
                
                // Try multiple ScraperAPI configurations for better success rate
                var scraperConfigs = new[]
                {
                    // Configuration 1: Standard academic site parameters
                    $"{baseUrl}?api_key={apiKey}&url={encodedUrl}&render=true&country_code=us&session_number=1&autoparse=true",
                    
                    // Configuration 2: Simplified parameters for problematic sites
                    $"{baseUrl}?api_key={apiKey}&url={encodedUrl}&render=true&country_code=us",
                    
                    // Configuration 3: Basic parameters only
                    $"{baseUrl}?api_key={apiKey}&url={encodedUrl}&render=true",
                    
                    // Configuration 4: No rendering for simple sites
                    $"{baseUrl}?api_key={apiKey}&url={encodedUrl}&country_code=us&session_number=1",
                    
                    // Configuration 5: Minimal parameters as last resort
                    $"{baseUrl}?api_key={apiKey}&url={encodedUrl}"
                };
                
                Exception lastException = null;
                
                for (int configIndex = 0; configIndex < scraperConfigs.Length; configIndex++)
                {
                    var scraperUrl = scraperConfigs[configIndex];
                    
                    try
                    {
                        Console.WriteLine($"[DEBUG] ScraperAPI attempt {configIndex + 1}/{scraperConfigs.Length}: {scraperUrl.Replace(apiKey, "***API_KEY***")}");
                        
                        using var handler = new HttpClientHandler()
                        {
                            AutomaticDecompression = System.Net.DecompressionMethods.None // Disable automatic decompression
                        };
                        
                        using var client = new HttpClient(handler);
                        client.Timeout = TimeSpan.FromMinutes(3); // ScraperAPI needs more time
                        
                        // Add headers to mimic real browser behavior
                        client.DefaultRequestHeaders.Clear();
                        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
                        client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                        client.DefaultRequestHeaders.Add("Pragma", "no-cache");
                        
                        var response = await client.GetAsync(scraperUrl);
                        Console.WriteLine($"[DEBUG] ScraperAPI Response: {response.StatusCode}");
                        Console.WriteLine($"[DEBUG] Response Content-Type: {response.Content.Headers.ContentType}");
                        Console.WriteLine($"[DEBUG] Response Content-Encoding: {response.Content.Headers.ContentEncoding}");
                        
                        if (response.IsSuccessStatusCode)
                        {
                            // Read content properly handling encoding
                            var content = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"[DEBUG] ScraperAPI content length: {content?.Length}");
                            
                            // Debug: Show first 500 characters of content to understand the structure
                            if (!string.IsNullOrEmpty(content))
                            {
                                var preview = content.Length > 500 ? content.Substring(0, 500) : content;
                                Console.WriteLine($"[DEBUG] ScraperAPI content preview: {preview}");
                                
                                // Check if content appears to be binary/compressed
                                var binaryCharCount = content.Take(100).Count(c => c < 32 && c != '\r' && c != '\n' && c != '\t');
                                if (binaryCharCount > 10)
                                {
                                    Console.WriteLine($"[DEBUG] Content appears to be binary/compressed (binary chars: {binaryCharCount}/100)");
                                    continue; // Try next configuration
                                }
                                
                                // More flexible HTML validation - check for any HTML-like content
                                if (content.Contains("<html", StringComparison.OrdinalIgnoreCase) || 
                                    content.Contains("<!doctype", StringComparison.OrdinalIgnoreCase) ||
                                    content.Contains("<title", StringComparison.OrdinalIgnoreCase) ||
                                    content.Contains("<head", StringComparison.OrdinalIgnoreCase) ||
                                    content.Contains("<body", StringComparison.OrdinalIgnoreCase) ||
                                    content.Contains("<div", StringComparison.OrdinalIgnoreCase) ||
                                    content.Contains("<meta", StringComparison.OrdinalIgnoreCase))
                                {
                                    Console.WriteLine($"[DEBUG] ScraperAPI succeeded with configuration {configIndex + 1}!");
                                    return content;
                                }
                                else
                                {
                                    Console.WriteLine($"[DEBUG] Configuration {configIndex + 1} returned invalid HTML content");
                                    lastException = new Exception($"Configuration {configIndex + 1} returned invalid HTML content");
                                    continue; // Try next configuration
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[DEBUG] Configuration {configIndex + 1} returned empty content");
                                lastException = new Exception($"Configuration {configIndex + 1} returned empty content");
                                continue;
                            }
                        }
                        else
                        {
                            var errorMessage = $"Configuration {configIndex + 1} returned: {response.StatusCode} - {response.ReasonPhrase}";
                            Console.WriteLine($"[DEBUG] {errorMessage}");
                            lastException = new Exception(errorMessage);
                            
                            // If we get 500 InternalServerError, wait a bit before trying next config
                            if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                            {
                                Console.WriteLine("[DEBUG] InternalServerError detected, waiting before next attempt...");
                                await Task.Delay(2000);
                            }
                            
                            continue; // Try next configuration
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] ScraperAPI configuration {configIndex + 1} failed: {ex.Message}");
                        lastException = ex;
                        
                        // Wait a bit before trying next configuration
                        if (configIndex < scraperConfigs.Length - 1)
                        {
                            await Task.Delay(1000);
                        }
                        continue;
                    }
                }
                
                // All configurations failed
                throw new Exception($"All ScraperAPI configurations failed. Last error: {lastException?.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] ScraperAPI failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Retrieves the HTML content of a given URL.
        /// Strategy: Always try direct access first to save ScraperAPI tokens, only fallback when blocked.
        /// </summary>
        /// <param name="url">The URL to fetch content from.</param>
        /// <returns>The HTML content as a string.</returns>
        /// <exception cref="Exception">Thrown if fetching the page content fails.</exception>
        private async Task<string> GetPageContentAsync(string url)
        {
            Exception? lastException = null;
            
            // STEP 1: Always try direct access first to save ScraperAPI tokens
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

            // STEP 2: Only use ScraperAPI as fallback when direct access fails (to save tokens)
            // This ensures we only pay for ScraperAPI when absolutely necessary
            try
            {
                var scraperContent = await GetPageContentWithScraperAPI(url);
                if (!string.IsNullOrEmpty(scraperContent))
                {
                    return scraperContent;
                }
            }
            catch (Exception scraperEx)
            {
                Console.WriteLine($"[DEBUG] ScraperAPI also failed: {scraperEx.Message}");
            }

            // If ScraperAPI failed, try enhanced alternative strategies
            Console.WriteLine("[DEBUG] Trying enhanced alternative URL and access strategies...");
            
            // Strategy 1: Try alternative URL patterns
            var altContent = await TryAlternativeUrlStrategies(url);
            if (!string.IsNullOrEmpty(altContent))
            {
                return altContent;
            }
            
            // Strategy 2: Try different proxy approaches
            await TryProxyStrategies(url);
            
            // Strategy 3: Try different browser simulation approaches
            var browserContent = await TryAdvancedBrowserSimulation(url);
            if (!string.IsNullOrEmpty(browserContent))
            {
                return browserContent;
            }
            
            // Strategy 4: Try minimal headers fallback
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

        #region Website Detection Methods
        
        /// <summary>
        /// Checks if the URL is from ResearchGate (kept for specialized metadata extraction).
        /// </summary>
        /// <param name="url">URL to check</param>
        /// <returns>True if URL is from ResearchGate</returns>
        private bool IsResearchGateUrl(string url) => url.Contains("researchgate.net");

        private bool IsArxivUrl(string url) => url.Contains("arxiv.org");
        private bool IsPubMedUrl(string url) => url.Contains("pubmed.ncbi.nlm.nih.gov") || url.Contains("ncbi.nlm.nih.gov");
        private bool IsIEEEUrl(string url) => url.Contains("ieeexplore.ieee.org") || url.Contains("ieee.org");
        private bool IsSpringerUrl(string url) => url.Contains("link.springer.com") || url.Contains("springer.com");
        private bool IsGoogleScholarUrl(string url) => url.Contains("scholar.google.com");
        private bool IsJSTORUrl(string url) => url.Contains("jstor.org");
        private bool IsScienceDirectUrl(string url) => url.Contains("sciencedirect.com");

        #endregion

        #region Specialized Metadata Extractors

        /// <summary>
        /// Extracts metadata from arXiv papers.
        /// </summary>
        private DocumentMetadataDto ExtractArxivMetadata(HtmlDocument doc, string url)
        {
            var metadata = new DocumentMetadataDto { URL = url, Source = "arXiv" };

            // Extract title
            var titleElement = doc.DocumentNode.SelectSingleNode("//h1[@class='title mathjax']");
            if (titleElement != null)
            {
                metadata.Title = titleElement.InnerText?.Replace("Title:", "").Trim();
            }

            // Extract authors
            var authorsElement = doc.DocumentNode.SelectSingleNode("//div[@class='authors']");
            if (authorsElement != null)
            {
                var authorLinks = authorsElement.SelectNodes(".//a");
                if (authorLinks != null)
                {
                    var authors = authorLinks.Select(a => a.InnerText?.Trim()).Where(a => !string.IsNullOrEmpty(a)).ToList();
                    metadata.Authors = string.Join(", ", authors);
                    metadata.Author = authors.FirstOrDefault();
                }
            }

            // Extract abstract
            var abstractElement = doc.DocumentNode.SelectSingleNode("//blockquote[@class='abstract mathjax']");
            if (abstractElement != null)
            {
                metadata.Abstract = abstractElement.InnerText?.Replace("Abstract:", "").Trim();
                metadata.Description = metadata.Abstract;
            }

            // Extract arXiv ID and set DOI-like identifier
            var arxivMatch = System.Text.RegularExpressions.Regex.Match(url, @"abs/(\d+\.\d+)");
            if (arxivMatch.Success)
            {
                metadata.DOI = $"arXiv:{arxivMatch.Groups[1].Value}";
            }

            metadata.Publisher = "arXiv";
            metadata.RetrievedDate = DateTime.UtcNow;
            
            return metadata;
        }

        /// <summary>
        /// Extracts metadata from PubMed articles.
        /// </summary>
        private DocumentMetadataDto ExtractPubMedMetadata(HtmlDocument doc, string url)
        {
            var metadata = new DocumentMetadataDto { URL = url, Source = "PubMed" };

            // Extract title
            var titleElement = doc.DocumentNode.SelectSingleNode("//h1[@class='heading-title']") ??
                              doc.DocumentNode.SelectSingleNode("//meta[@name='citation_title']");
            
            if (titleElement != null)
            {
                metadata.Title = titleElement.Name == "meta" 
                    ? GetAttributeValue(titleElement, "content") 
                    : titleElement.InnerText?.Trim();
            }

            // Extract authors
            var authorElements = doc.DocumentNode.SelectNodes("//meta[@name='citation_author']");
            if (authorElements != null)
            {
                var authors = authorElements.Select(e => GetAttributeValue(e, "content")).Where(a => !string.IsNullOrEmpty(a)).ToList();
                metadata.Authors = string.Join(", ", authors);
                metadata.Author = authors.FirstOrDefault();
            }

            // Extract journal
            var journalElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_journal_title']");
            if (journalElement != null)
            {
                metadata.Journal = GetAttributeValue(journalElement, "content");
            }

            // Extract DOI
            var doiElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_doi']");
            if (doiElement != null)
            {
                metadata.DOI = GetAttributeValue(doiElement, "content");
            }

            // Extract publication date
            var dateElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_publication_date']");
            if (dateElement != null && DateTime.TryParse(GetAttributeValue(dateElement, "content"), out var pubDate))
            {
                metadata.PublicationDate = DateOnly.FromDateTime(pubDate);
            }

            // Extract abstract
            var abstractElement = doc.DocumentNode.SelectSingleNode("//div[@class='abstract-content selected']") ??
                                 doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
            
            if (abstractElement != null)
            {
                metadata.Abstract = abstractElement.Name == "meta" 
                    ? GetAttributeValue(abstractElement, "content") 
                    : abstractElement.InnerText?.Trim();
                metadata.Description = metadata.Abstract;
            }

            metadata.Publisher = "PubMed";
            metadata.RetrievedDate = DateTime.UtcNow;
            
            return metadata;
        }

        /// <summary>
        /// Extracts metadata from IEEE Xplore articles.
        /// </summary>
        private DocumentMetadataDto ExtractIEEEMetadata(HtmlDocument doc, string url)
        {
            var metadata = new DocumentMetadataDto { URL = url, Source = "IEEE Xplore" };

            // Extract title
            var titleElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_title']") ??
                              doc.DocumentNode.SelectSingleNode("//h1[@class='document-title']");
            
            if (titleElement != null)
            {
                metadata.Title = titleElement.Name == "meta" 
                    ? GetAttributeValue(titleElement, "content") 
                    : titleElement.InnerText?.Trim();
            }

            // Extract authors
            var authorElements = doc.DocumentNode.SelectNodes("//meta[@name='citation_author']");
            if (authorElements != null)
            {
                var authors = authorElements.Select(e => GetAttributeValue(e, "content")).Where(a => !string.IsNullOrEmpty(a)).ToList();
                metadata.Authors = string.Join(", ", authors);
                metadata.Author = authors.FirstOrDefault();
            }

            // Extract conference/journal
            var venueElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_conference_title']") ??
                              doc.DocumentNode.SelectSingleNode("//meta[@name='citation_journal_title']");
            
            if (venueElement != null)
            {
                metadata.Journal = GetAttributeValue(venueElement, "content");
            }

            // Extract DOI
            var doiElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_doi']");
            if (doiElement != null)
            {
                metadata.DOI = GetAttributeValue(doiElement, "content");
            }

            // Extract publication date
            var dateElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_publication_date']");
            if (dateElement != null && DateTime.TryParse(GetAttributeValue(dateElement, "content"), out var pubDate))
            {
                metadata.PublicationDate = DateOnly.FromDateTime(pubDate);
            }

            // Extract abstract
            var abstractElement = doc.DocumentNode.SelectSingleNode("//div[@class='abstract-text']") ??
                                 doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
            
            if (abstractElement != null)
            {
                metadata.Abstract = abstractElement.Name == "meta" 
                    ? GetAttributeValue(abstractElement, "content") 
                    : abstractElement.InnerText?.Trim();
                metadata.Description = metadata.Abstract;
            }

            metadata.Publisher = "IEEE";
            metadata.RetrievedDate = DateTime.UtcNow;
            
            return metadata;
        }

        /// <summary>
        /// Extracts metadata from Springer articles.
        /// </summary>
        private DocumentMetadataDto ExtractSpringerMetadata(HtmlDocument doc, string url)
        {
            var metadata = new DocumentMetadataDto { URL = url, Source = "Springer" };

            // Extract title
            var titleElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_title']") ??
                              doc.DocumentNode.SelectSingleNode("//h1[@class='c-article-title']");
            
            if (titleElement != null)
            {
                metadata.Title = titleElement.Name == "meta" 
                    ? GetAttributeValue(titleElement, "content") 
                    : titleElement.InnerText?.Trim();
            }

            // Extract authors
            var authorElements = doc.DocumentNode.SelectNodes("//meta[@name='citation_author']");
            if (authorElements != null)
            {
                var authors = authorElements.Select(e => GetAttributeValue(e, "content")).Where(a => !string.IsNullOrEmpty(a)).ToList();
                metadata.Authors = string.Join(", ", authors);
                metadata.Author = authors.FirstOrDefault();
            }

            // Extract journal
            var journalElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_journal_title']");
            if (journalElement != null)
            {
                metadata.Journal = GetAttributeValue(journalElement, "content");
            }

            // Extract DOI
            var doiElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_doi']");
            if (doiElement != null)
            {
                metadata.DOI = GetAttributeValue(doiElement, "content");
            }

            // Extract publication date
            var dateElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_publication_date']");
            if (dateElement != null && DateTime.TryParse(GetAttributeValue(dateElement, "content"), out var pubDate))
            {
                metadata.PublicationDate = DateOnly.FromDateTime(pubDate);
            }

            metadata.Publisher = "Springer";
            metadata.RetrievedDate = DateTime.UtcNow;
            
            return metadata;
        }

        /// <summary>
        /// Extracts metadata from Google Scholar (limited due to blocking).
        /// </summary>
        private DocumentMetadataDto ExtractGoogleScholarMetadata(HtmlDocument doc, string url)
        {
            var metadata = new DocumentMetadataDto { URL = url, Source = "Google Scholar" };
            
            // Google Scholar heavily blocks automated access, so this is mostly a fallback
            var titleElement = doc.DocumentNode.SelectSingleNode("//title");
            if (titleElement != null)
            {
                metadata.Title = titleElement.InnerText?.Replace(" - Google Scholar", "").Trim();
            }

            metadata.Publisher = "Google Scholar";
            metadata.RetrievedDate = DateTime.UtcNow;
            
            return metadata;
        }

        /// <summary>
        /// Extracts metadata from JSTOR articles.
        /// </summary>
        private DocumentMetadataDto ExtractJSTORMetadata(HtmlDocument doc, string url)
        {
            var metadata = new DocumentMetadataDto { URL = url, Source = "JSTOR" };

            // Extract title
            var titleElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_title']") ??
                              doc.DocumentNode.SelectSingleNode("//h1[@class='title']");
            
            if (titleElement != null)
            {
                metadata.Title = titleElement.Name == "meta" 
                    ? GetAttributeValue(titleElement, "content") 
                    : titleElement.InnerText?.Trim();
            }

            // Extract authors
            var authorElements = doc.DocumentNode.SelectNodes("//meta[@name='citation_author']");
            if (authorElements != null)
            {
                var authors = authorElements.Select(e => GetAttributeValue(e, "content")).Where(a => !string.IsNullOrEmpty(a)).ToList();
                metadata.Authors = string.Join(", ", authors);
                metadata.Author = authors.FirstOrDefault();
            }

            // Extract journal
            var journalElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_journal_title']");
            if (journalElement != null)
            {
                metadata.Journal = GetAttributeValue(journalElement, "content");
            }

            // Extract DOI
            var doiElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_doi']");
            if (doiElement != null)
            {
                metadata.DOI = GetAttributeValue(doiElement, "content");
            }

            metadata.Publisher = "JSTOR";
            metadata.RetrievedDate = DateTime.UtcNow;
            
            return metadata;
        }

        /// <summary>
        /// Extracts metadata from ScienceDirect articles.
        /// </summary>
        private DocumentMetadataDto ExtractScienceDirectMetadata(HtmlDocument doc, string url)
        {
            var metadata = new DocumentMetadataDto { URL = url, Source = "ScienceDirect" };

            // Extract title
            var titleElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_title']") ??
                              doc.DocumentNode.SelectSingleNode("//h1[@class='title-text']");
            
            if (titleElement != null)
            {
                metadata.Title = titleElement.Name == "meta" 
                    ? GetAttributeValue(titleElement, "content") 
                    : titleElement.InnerText?.Trim();
            }

            // Extract authors
            var authorElements = doc.DocumentNode.SelectNodes("//meta[@name='citation_author']");
            if (authorElements != null)
            {
                var authors = authorElements.Select(e => GetAttributeValue(e, "content")).Where(a => !string.IsNullOrEmpty(a)).ToList();
                metadata.Authors = string.Join(", ", authors);
                metadata.Author = authors.FirstOrDefault();
            }

            // Extract journal
            var journalElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_journal_title']");
            if (journalElement != null)
            {
                metadata.Journal = GetAttributeValue(journalElement, "content");
            }

            // Extract DOI
            var doiElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_doi']");
            if (doiElement != null)
            {
                metadata.DOI = GetAttributeValue(doiElement, "content");
            }

            // Extract publication date
            var dateElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_publication_date']");
            if (dateElement != null && DateTime.TryParse(GetAttributeValue(dateElement, "content"), out var pubDate))
            {
                metadata.PublicationDate = DateOnly.FromDateTime(pubDate);
            }

            metadata.Publisher = "Elsevier";
            metadata.RetrievedDate = DateTime.UtcNow;
            
            return metadata;
        }

        /// <summary>
        /// Extracts standard web metadata using common meta tags.
        /// </summary>
        private DocumentMetadataDto ExtractStandardWebMetadata(HtmlDocument doc, string url)
        {
            Console.WriteLine($"[DEBUG] ExtractStandardWebMetadata called for: {url}");
            var metadata = new DocumentMetadataDto { URL = url };

            // Extract title
            var titleSelectors = new[]
            {
                "meta[property='og:title']",
                "meta[name='twitter:title']",
                "title",
                "h1"
            };

            Console.WriteLine("[DEBUG] Extracting title using standard selectors...");
            foreach (var selector in titleSelectors)
            {
                var titleElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                Console.WriteLine($"[DEBUG] Title selector '{selector}' found: {titleElement != null}");
                
                if (titleElement != null)
                {
                    var title = titleElement.Name == "meta" 
                        ? GetAttributeValue(titleElement, "content") 
                        : titleElement.InnerText?.Trim();
                    
                    Console.WriteLine($"[DEBUG] Extracted title: '{title}' (length: {title?.Length ?? 0})");
                    if (!string.IsNullOrEmpty(title) && title.Length > 5)
                    {
                        metadata.Title = title;
                        Console.WriteLine($"[DEBUG] Title accepted: {title}");
                        break;
                    }
                }
            }

            // Extract description/abstract
            Console.WriteLine("[DEBUG] Extracting description...");
            var descriptionSelectors = new[]
            {
                "meta[property='og:description']",
                "meta[name='description']",
                "meta[name='twitter:description']"
            };

            foreach (var selector in descriptionSelectors)
            {
                var descElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                Console.WriteLine($"[DEBUG] Description selector '{selector}' found: {descElement != null}");
                if (descElement != null)
                {
                    var description = GetAttributeValue(descElement, "content");
                    Console.WriteLine($"[DEBUG] Description content: '{description?.Substring(0, Math.Min(100, description?.Length ?? 0))}...'");
                    if (!string.IsNullOrEmpty(description))
                    {
                        metadata.Description = description;
                        metadata.Abstract = description;
                        break;
                    }
                }
            }

            // Extract site name/source
            Console.WriteLine("[DEBUG] Extracting site name...");
            var siteElement = doc.DocumentNode.SelectSingleNode("//meta[@property='og:site_name']");
            Console.WriteLine($"[DEBUG] Site name element found: {siteElement != null}");
            if (siteElement != null)
            {
                metadata.Source = GetAttributeValue(siteElement, "content");
                metadata.Publisher = metadata.Source;
                Console.WriteLine($"[DEBUG] Site name: {metadata.Source}");
            }

            // Extract author
            Console.WriteLine("[DEBUG] Extracting author...");
            var authorElement = doc.DocumentNode.SelectSingleNode("//meta[@name='author']");
            Console.WriteLine($"[DEBUG] Author element found: {authorElement != null}");
            if (authorElement != null)
            {
                var author = GetAttributeValue(authorElement, "content");
                Console.WriteLine($"[DEBUG] Author: {author}");
                if (!string.IsNullOrEmpty(author))
                {
                    metadata.Author = author;
                    metadata.Authors = author;
                }
            }

            // Extract keywords
            Console.WriteLine("[DEBUG] Extracting keywords...");
            var keywordsElement = doc.DocumentNode.SelectSingleNode("//meta[@name='keywords']");
            Console.WriteLine($"[DEBUG] Keywords element found: {keywordsElement != null}");
            if (keywordsElement != null)
            {
                metadata.Keywords = GetAttributeValue(keywordsElement, "content");
                Console.WriteLine($"[DEBUG] Keywords: {metadata.Keywords}");
            }

            // Extract language
            Console.WriteLine("[DEBUG] Extracting language...");
            var langElement = doc.DocumentNode.SelectSingleNode("//html[@lang]");
            metadata.Language = langElement != null ? GetAttributeValue(langElement, "lang") ?? "vi" : "vi";
            Console.WriteLine($"[DEBUG] Language: {metadata.Language}");

            // Set retrieved date
            metadata.RetrievedDate = DateTime.UtcNow;

            // Fallback for title
            if (string.IsNullOrEmpty(metadata.Title))
            {
                var uri = new Uri(url);
                metadata.Title = $"Tài liệu từ {uri.Host}";
                Console.WriteLine($"[DEBUG] Using fallback title: {metadata.Title}");
            }

            Console.WriteLine($"[DEBUG] ExtractStandardWebMetadata completed. Final metadata:");
            Console.WriteLine($"[DEBUG] - Title: {metadata.Title}");
            Console.WriteLine($"[DEBUG] - Description: {metadata.Description?.Substring(0, Math.Min(100, metadata.Description?.Length ?? 0))}...");
            Console.WriteLine($"[DEBUG] - Source: {metadata.Source}");
            Console.WriteLine($"[DEBUG] - Author: {metadata.Author}");

            return metadata;
        }

        #endregion

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