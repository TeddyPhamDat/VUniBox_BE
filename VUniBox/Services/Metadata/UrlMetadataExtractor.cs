using HtmlAgilityPack;
using VUniBox.Models.Enum;
using System.Text.RegularExpressions;
using VUniBox.Models.DTO;
using System.Web;
using System.IO.Compression;
using System.Collections.Concurrent;
using System.Diagnostics;

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
                    return "Tài liệu";
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
                return "Trang web";
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
        
        // Performance optimization: Cache recent results to avoid duplicate requests
        private static readonly ConcurrentDictionary<string, (DocumentMetadataDto metadata, DateTime expiry)> _cache 
            = new ConcurrentDictionary<string, (DocumentMetadataDto, DateTime)>();
        
        // Performance optimization: Track failed URLs to avoid retrying immediately
        private static readonly ConcurrentDictionary<string, DateTime> _failedUrls 
            = new ConcurrentDictionary<string, DateTime>();
        
        // Configuration for timeout and cache
        private static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromSeconds(15); // Reduced from default
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan FAILED_URL_COOLDOWN = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Initializes a new instance of the <see cref="UrlMetadataExtractor"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for web requests.</param>
        /// <param name="configuration">The configuration to get ScraperAPI settings.</param>
        public UrlMetadataExtractor(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            
            // Configure HttpClient for performance
            _httpClient.Timeout = REQUEST_TIMEOUT;
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
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Clean and validate URL first
                url = CleanAndValidateUrl(url);
                Console.WriteLine($"[PERF] Starting metadata extraction for: {url}");
                
                // Performance optimization: Check cache first
                var cacheKey = $"{url}_{documentType}";
                if (_cache.TryGetValue(cacheKey, out var cached) && cached.expiry > DateTime.UtcNow)
                {
                    Console.WriteLine($"[PERF] Cache hit for {url} - took {stopwatch.ElapsedMilliseconds}ms");
                    return cached.metadata;
                }
                
                // Performance optimization: Check if URL recently failed
                if (_failedUrls.TryGetValue(url, out var failTime) && 
                    DateTime.UtcNow - failTime < FAILED_URL_COOLDOWN)
                {
                    Console.WriteLine($"[PERF] URL recently failed, using fallback immediately: {url}");
                    return CreateFallbackMetadata(url, documentType, "Recently failed - using cached fallback");
                }
                
                DocumentMetadataDto result;
                
                // Use timeout wrapper for all extraction methods
                using (var cts = new CancellationTokenSource(REQUEST_TIMEOUT))
                {
                    switch (documentType)
                    {
                        case DocumentType.Research:
                            result = await ExtractFromResearchSiteAsync(url).ConfigureAwait(false);
                            break;
                        case DocumentType.Book:
                            result = await ExtractFromBookSiteAsync(url).ConfigureAwait(false);
                            break;
                        case DocumentType.Newspaper:
                            result = await ExtractFromNewsSiteAsync(url).ConfigureAwait(false);
                            break;
                        default:
                            result = await ExtractGenericMetadataAsync(url).ConfigureAwait(false);
                            break;
                    }
                }
                
                // Cache successful result
                _cache.TryAdd(cacheKey, (result, DateTime.UtcNow.Add(CACHE_DURATION)));
                
                // Clean old cache entries periodically
                if (_cache.Count > 100)
                {
                    CleanExpiredCache();
                }
                
                Console.WriteLine($"[PERF] Successfully extracted metadata for {url} - took {stopwatch.ElapsedMilliseconds}ms");
                return result;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[PERF] Timeout after {stopwatch.ElapsedMilliseconds}ms for {url}");
                _failedUrls.TryAdd(url, DateTime.UtcNow);
                return CreateFallbackMetadata(url, documentType, "Request timeout");
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            {
                Console.WriteLine($"[PERF] Access denied for {url} after {stopwatch.ElapsedMilliseconds}ms, creating fallback metadata");
                _failedUrls.TryAdd(url, DateTime.UtcNow);
                return CreateFallbackMetadata(url, documentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PERF] Failed to extract metadata from {url} after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                _failedUrls.TryAdd(url, DateTime.UtcNow);
                return CreateFallbackMetadata(url, documentType, ex.Message);
            }
        }
        
        /// <summary>
        /// Cleans expired entries from cache to prevent memory leaks
        /// </summary>
        private static void CleanExpiredCache()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _cache.Where(kvp => kvp.Value.expiry < now).Select(kvp => kvp.Key).ToList();
            
            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }
            
            // Also clean failed URLs that are past cooldown
            var expiredFailedUrls = _failedUrls.Where(kvp => now - kvp.Value > FAILED_URL_COOLDOWN)
                                              .Select(kvp => kvp.Key).ToList();
            
            foreach (var url in expiredFailedUrls)
            {
                _failedUrls.TryRemove(url, out _);
            }
            
            Console.WriteLine($"[PERF] Cleaned {expiredKeys.Count} expired cache entries and {expiredFailedUrls.Count} expired failed URLs");
        }
        
        /// <summary>
        /// Force clear all cache entries (useful for clearing old Vietnamese fallback metadata)
        /// </summary>
        public static void ClearAllCache()
        {
            var cacheCount = _cache.Count;
            var failedCount = _failedUrls.Count;
            
            _cache.Clear();
            _failedUrls.Clear();
            
            Console.WriteLine($"[PERF] Force cleared {cacheCount} cache entries and {failedCount} failed URLs");
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
        /// Creates intelligent fallback metadata when extraction fails, with smart title extraction and professional descriptions.
        /// </summary>
        /// <param name="url">The URL that failed to extract.</param>
        /// <param name="documentType">The type of document.</param>
        /// <param name="errorMessage">Optional error message for debugging.</param>
        /// <returns>A fallback DocumentMetadataDto with intelligent information.</returns>
        private DocumentMetadataDto CreateFallbackMetadata(string url, DocumentType documentType, string errorMessage = null)
        {
            var uri = new Uri(url);
            var domain = uri.Host.ToLowerInvariant();
            
            // Extract intelligent title from URL structure
            var extractedTitle = ExtractTitleFromUrl(url, domain);
            
            // Determine source and publication type based on domain
            var (source, publicationType) = DetermineSourceAndType(domain, documentType);
            
            var metadata = new DocumentMetadataDto
            {
                URL = url,
                Title = extractedTitle ?? $"{publicationType} từ {source}",
                Source = source,
                Language = domain.Contains(".vn") ? "vi" : "en", // Detect Vietnamese sites
                RetrievedDate = DateTime.UtcNow
            };
            
            // Create intelligent descriptions based on site type
            if (domain.Contains("researchgate"))
            {
                metadata.Description = "Nghiên cứu học thuật từ ResearchGate - Mạng xã hội dành cho các nhà khoa học và nghiên cứu";
                metadata.Abstract = "Bài nghiên cứu học thuật - tóm tắt có sẵn trên nền tảng ResearchGate";
                metadata.Publisher = "ResearchGate";
                
                // Try to extract more info from ResearchGate URL pattern
                var rgMatch = System.Text.RegularExpressions.Regex.Match(url, @"publication/(\d+)_(.+)");
                if (rgMatch.Success)
                {
                    var publicationId = rgMatch.Groups[1].Value;
                    metadata.DOI = $"RG:{publicationId}";
                    
                    // If title wasn't extracted, try again with better parsing
                    if (metadata.Title.StartsWith("Research") || metadata.Title.Contains("từ"))
                    {
                        var titlePart = rgMatch.Groups[2].Value;
                        var cleanTitle = titlePart.Replace('_', ' ')
                                                 .Replace('-', ' ')
                                                 .Replace("%20", " ");
                        cleanTitle = System.Text.RegularExpressions.Regex.Replace(cleanTitle, @"\s+", " ");
                        cleanTitle = System.Web.HttpUtility.UrlDecode(cleanTitle);
                        
                        // Smart title case with proper handling of acronyms
                        cleanTitle = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanTitle.ToLower());
                        
                        // Fix common acronyms that should be uppercase
                        cleanTitle = System.Text.RegularExpressions.Regex.Replace(cleanTitle, @"\bAi\b", "AI");
                        cleanTitle = System.Text.RegularExpressions.Regex.Replace(cleanTitle, @"\bIt\b", "IT");
                        cleanTitle = System.Text.RegularExpressions.Regex.Replace(cleanTitle, @"\bUi\b", "UI");
                        cleanTitle = System.Text.RegularExpressions.Regex.Replace(cleanTitle, @"\bUx\b", "UX");
                        cleanTitle = System.Text.RegularExpressions.Regex.Replace(cleanTitle, @"\bIoT\b", "IoT");
                        cleanTitle = System.Text.RegularExpressions.Regex.Replace(cleanTitle, @"\bNlp\b", "NLP");
                        cleanTitle = System.Text.RegularExpressions.Regex.Replace(cleanTitle, @"\bApi\b", "API");
                        
                        metadata.Title = cleanTitle;
                    }
                }
            }
            else if (domain.Contains("arxiv"))
            {
                metadata.Description = "Bài báo khoa học từ arXiv - Kho lưu trữ tiền ấn phẩm cho vật lý, toán học, khoa học máy tính";
                metadata.Abstract = "Bài báo khoa học tiền ấn phẩm - toàn văn có sẵn trên arXiv";
                metadata.Publisher = "arXiv";
                metadata.Language = "en";
            }
            else if (domain.Contains("ieee"))
            {
                metadata.Description = "Tài liệu kỹ thuật từ IEEE - Viện Kỹ sư Điện và Điện tử";
                metadata.Abstract = "Bài báo kỹ thuật IEEE - nội dung đầy đủ dành cho người đăng ký";
                metadata.Publisher = "IEEE";
            }
            else if (domain.Contains("springer"))
            {
                metadata.Description = "Nghiên cứu học thuật từ Springer Nature - Nhà xuất bản khoa học quốc tế";
                metadata.Abstract = "Bài báo nghiên cứu được xuất bản bởi Springer Nature";
                metadata.Publisher = "Springer Nature";
            }
            else if (domain.Contains("pubmed"))
            {
                metadata.Description = "Nghiên cứu y sinh từ PubMed - Cơ sở dữ liệu y học của NIH";
                metadata.Abstract = "Bài nghiên cứu y sinh học - tóm tắt có sẵn trong PubMed";
                metadata.Publisher = "PubMed/NCBI";
            }
            else
            {
                // Generic fallback for other sites
                metadata.Description = $"Tài liệu từ {source} - có thể cần đăng ký hoặc đăng nhập để truy cập";
                metadata.Abstract = $"Tài liệu được lưu trữ trên nền tảng {source}";
                metadata.Language = domain.Contains(".vn") ? "vi" : "en";
            }
            
            // Add debug information if error message is provided
            if (!string.IsNullOrEmpty(errorMessage))
            {
                // Determine if the error is related to ScraperAPI or network issues
                if (errorMessage.Contains("ScraperAPI") || errorMessage.Contains("api_key"))
                {
                    Console.WriteLine($"[PERF] Tạo thông tin fallback cho {domain} do ScraperAPI gặp vấn đề. Tiêu đề: '{metadata.Title}'");
                    metadata.Description += " (Lưu ý: Dịch vụ trích xuất tự động tạm thời gặp sự cố, thông tin hiển thị là cơ bản)";
                }
                else if (errorMessage.Contains("timeout") || errorMessage.Contains("Request timeout"))
                {
                    Console.WriteLine($"[PERF] Tạo thông tin fallback cho {domain} do timeout. Tiêu đề: '{metadata.Title}'");
                    metadata.Description += " (Lưu ý: Trang web phản hồi chậm, thông tin hiển thị là cơ bản)";
                }
                else
                {
                    Console.WriteLine($"[PERF] Tạo thông tin fallback cho {domain}. Tiêu đề: '{metadata.Title}' | Lỗi: {errorMessage}");
                }
            }
            else
            {
                Console.WriteLine($"[PERF] Tạo thông tin fallback thông minh cho {domain}. Tiêu đề: '{metadata.Title}'");
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
                    var match = System.Text.RegularExpressions.Regex.Match(url, @"publication/\d+_(.+?)(?:\?|#|$)");
                    if (match.Success)
                    {
                        var title = match.Groups[1].Value;
                        
                        // Clean up URL encoding and formatting
                        title = System.Web.HttpUtility.UrlDecode(title);
                        title = title.Replace('_', ' ').Replace('-', ' ').Replace("%20", " ");
                        
                        // Remove common URL artifacts and clean whitespace
                        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+", " ");
                        title = title.Trim();
                        
                        // Convert to proper title case for better readability
                        if (!string.IsNullOrEmpty(title))
                        {
                            title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(title.ToLower());
                            
                            // Fix common acronyms that should be uppercase
                            title = System.Text.RegularExpressions.Regex.Replace(title, @"\bAi\b", "AI");
                            title = System.Text.RegularExpressions.Regex.Replace(title, @"\bIt\b", "IT");
                            title = System.Text.RegularExpressions.Regex.Replace(title, @"\bUi\b", "UI");
                            title = System.Text.RegularExpressions.Regex.Replace(title, @"\bUx\b", "UX");
                            title = System.Text.RegularExpressions.Regex.Replace(title, @"\bIoT\b", "IoT");
                            title = System.Text.RegularExpressions.Regex.Replace(title, @"\bNlp\b", "NLP");
                            title = System.Text.RegularExpressions.Regex.Replace(title, @"\bApi\b", "API");
                            title = System.Text.RegularExpressions.Regex.Replace(title, @"\bXi\b", "XI"); // For "Region XI"
                            
                            Console.WriteLine($"[PERF] Extracted ResearchGate title: '{title}'");
                            return title;
                        }
                    }
                    
                    // Fallback: try to extract just the numeric ID for ResearchGate
                    var idMatch = System.Text.RegularExpressions.Regex.Match(url, @"publication/(\d+)");
                    if (idMatch.Success)
                    {
                        return $"ResearchGate Publication {idMatch.Groups[1].Value}";
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
        /// Try alternative URL strategies for blocked sites
        /// </summary>
        private async Task<string?> TryAlternativeUrlStrategies(string originalUrl)
        {
            var strategies = new List<string>();
            
            // ResearchGate alternatives
            if (originalUrl.Contains("researchgate.net"))
            {
                // Note: m.researchgate.net doesn't exist, only try working alternatives
                
                // Extract publication ID and try alternative patterns
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
        /// Retrieves HTML content using ScraperAPI with optimized fast-fail strategies.
        /// </summary>
        /// <param name="url">The URL to fetch content from.</param>
        /// <returns>The HTML content as a string.</returns>
        private async Task<string> GetPageContentWithScraperAPI(string url)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var apiKey = _configuration["ScraperAPI:ApiKey"];
                var baseUrl = _configuration["ScraperAPI:BaseUrl"];
                
                Console.WriteLine($"[DEBUG] ScraperAPI Config - ApiKey: {(string.IsNullOrEmpty(apiKey) ? "MISSING" : "PRESENT")}, BaseUrl: {baseUrl ?? "MISSING"}");
                
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("[PERF] ScraperAPI key not configured - falling back to basic extraction");
                    throw new Exception("ScraperAPI không được cấu hình - sử dụng trích xuất cơ bản");
                }
                
                if (string.IsNullOrEmpty(baseUrl))
                {
                    Console.WriteLine("[PERF] ScraperAPI BaseUrl not configured - using default");
                    baseUrl = "http://api.scraperapi.com";
                }

                var encodedUrl = Uri.EscapeDataString(url);
                Console.WriteLine($"[PERF] Starting ScraperAPI for: {url}");
                
                // Fast-fail: Try multiple configurations in parallel with shorter timeouts
                var configurations = new[]
                {
                    // Fast config - minimal parameters for speed
                    $"{baseUrl}?api_key={apiKey}&url={encodedUrl}&render=false",
                    
                    // Standard config - basic rendering
                    $"{baseUrl}?api_key={apiKey}&url={encodedUrl}&render=true&country_code=us",
                    
                    // Premium config - for difficult sites (only if needed)
                    $"{baseUrl}?api_key={apiKey}&url={encodedUrl}&render=true&premium_proxy=true&country_code=us&session_number=1"
                };

                // Use CancellationToken for timeout control
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12)); // Reduced from 20s to 12s total
                
                // Try configurations in order, but don't wait too long for each
                foreach (var scraperUrl in configurations)
                {
                    try
                    {
                        Console.WriteLine($"[PERF] Trying ScraperAPI config #{Array.IndexOf(configurations, scraperUrl) + 1}");
                        
                        using var client = new HttpClient();
                        client.Timeout = TimeSpan.FromSeconds(5); // Reduced from 8s to 5s for faster fail
                        
                        // Minimal headers for speed
                        client.DefaultRequestHeaders.Add("Accept", "text/html");
                        
                        var response = await client.GetAsync(scraperUrl, cts.Token).ConfigureAwait(false);
                        
                        Console.WriteLine($"[DEBUG] ScraperAPI response: {response.StatusCode} | Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}"))}");
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            Console.WriteLine($"[DEBUG] ScraperAPI content length: {content?.Length ?? 0}");
                            
                            // Quick validation
                            if (!string.IsNullOrEmpty(content) && content.Contains("<", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"[PERF] ScraperAPI succeeded with config #{Array.IndexOf(configurations, scraperUrl) + 1} in {stopwatch.ElapsedMilliseconds}ms");
                                return content;
                            }
                            else
                            {
                                Console.WriteLine($"[DEBUG] ScraperAPI returned non-HTML content: {content?.Substring(0, Math.Min(200, content?.Length ?? 0))}");
                            }
                        }
                        
                        Console.WriteLine($"[PERF] Config #{Array.IndexOf(configurations, scraperUrl) + 1} failed: {response.StatusCode}");
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"[PERF] ScraperAPI config #{Array.IndexOf(configurations, scraperUrl) + 1} timed out");
                        break; // Don't try more configs if we're timing out
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PERF] ScraperAPI config #{Array.IndexOf(configurations, scraperUrl) + 1} error: {ex.Message}");
                    }
                }
                
                throw new Exception($"ScraperAPI không thể truy cập trang web sau {stopwatch.ElapsedMilliseconds}ms - sử dụng thông tin cơ bản");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PERF] ScraperAPI hoàn toàn thất bại sau {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Retrieves the HTML content of a given URL with optimized parallel strategies.
        /// Strategy: Try direct access and alternative strategies in parallel for speed.
        /// </summary>
        /// <param name="url">The URL to fetch content from.</param>
        /// <returns>The HTML content as a string.</returns>
        /// <exception cref="Exception">Thrown if fetching the page content fails.</exception>
        private async Task<string> GetPageContentAsync(string url)
        {
            var stopwatch = Stopwatch.StartNew();
            Exception? lastException = null;
            
            try
            {
                Console.WriteLine($"[PERF] Starting content fetch for: {url}");
                
                // Performance optimization: Try multiple strategies in parallel with fast timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12)); // Reduced overall timeout
                
                // Create multiple fetch strategies to run in parallel
                var tasks = new List<Task<string>>();
                
                // Strategy 1: Direct access with minimal headers (fastest)
                tasks.Add(TryDirectAccess(url, cts.Token));
                
                // Strategy 2: Alternative URL patterns (for academic sites)
                if (IsAcademicSite(url))
                {
                    tasks.Add(TryAlternativeAcademicUrls(url, cts.Token));
                }
                
                // Strategy 3: Browser simulation (fallback)
                tasks.Add(TryBrowserSimulation(url, cts.Token));
                
                // Wait for first successful result
                while (tasks.Count > 0)
                {
                    var completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                    tasks.Remove(completedTask);
                    
                    try
                    {
                        var result = await completedTask.ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(result))
                        {
                            Console.WriteLine($"[PERF] Content fetch succeeded in {stopwatch.ElapsedMilliseconds}ms");
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        Console.WriteLine($"[PERF] Strategy failed: {ex.Message}");
                    }
                }
                
                // If all direct methods fail, try ScraperAPI as last resort
                Console.WriteLine($"[PERF] Các phương thức trực tiếp thất bại sau {stopwatch.ElapsedMilliseconds}ms, thử ScraperAPI");
                return await GetPageContentWithScraperAPI(url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PERF] All methods failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                throw new Exception($"Failed to fetch content for {url} after {stopwatch.ElapsedMilliseconds}ms", lastException ?? ex);
            }
        }
        
        /// <summary>
        /// Fast direct access attempt with minimal headers
        /// </summary>
        private async Task<string> TryDirectAccess(string url, CancellationToken cancellationToken)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(6); // Short timeout for speed
            
            // Minimal headers for fastest response
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html");
            
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(content) && content.Contains("<"))
                {
                    Console.WriteLine("[PERF] Direct access succeeded");
                    return content;
                }
            }
            
            throw new Exception($"Direct access failed: {response.StatusCode}");
        }
        
        /// <summary>
        /// Try alternative URLs for academic sites
        /// </summary>
        private async Task<string> TryAlternativeAcademicUrls(string url, CancellationToken cancellationToken)
        {
            var alternatives = new List<string>();
            
            // ResearchGate alternatives
            if (url.Contains("researchgate.net"))
            {
                alternatives.Add(url.Replace("www.researchgate.net", "m.researchgate.net"));
                alternatives.Add(url.Replace("publication/", "profile/"));
            }
            
            // arXiv alternatives
            if (url.Contains("arxiv.org"))
            {
                if (url.Contains("/abs/"))
                {
                    alternatives.Add(url.Replace("/abs/", "/pdf/") + ".pdf");
                }
            }
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            foreach (var altUrl in alternatives)
            {
                try
                {
                    var response = await client.GetAsync(altUrl, cancellationToken).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(content) && content.Contains("<"))
                        {
                            Console.WriteLine($"[PERF] Alternative URL succeeded: {altUrl}");
                            return content;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PERF] Alternative {altUrl} failed: {ex.Message}");
                }
            }
            
            throw new Exception("No alternative URLs succeeded");
        }
        
        /// <summary>
        /// Browser simulation with realistic headers
        /// </summary>
        private async Task<string> TryBrowserSimulation(string url, CancellationToken cancellationToken)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            
            // Realistic browser headers
            client.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", 
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "vi,en-US;q=0.9,en;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
            client.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
            client.DefaultRequestHeaders.Add("sec-fetch-site", "none");
            client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");
            
            // Special handling for ResearchGate
            if (url.Contains("researchgate.net"))
            {
                client.DefaultRequestHeaders.Add("Referer", "https://scholar.google.com/");
            }
            
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(content) && content.Contains("<"))
                {
                    Console.WriteLine("[PERF] Browser simulation succeeded");
                    return content;
                }
            }
            
            throw new Exception($"Browser simulation failed: {response.StatusCode}");
        }
        
        /// <summary>
        /// Check if URL is from an academic site
        /// </summary>
        private bool IsAcademicSite(string url)
        {
            return url.Contains("researchgate.net") || 
                   url.Contains("arxiv.org") || 
                   url.Contains("ieee.org") || 
                   url.Contains("springer.com") || 
                   url.Contains("pubmed") || 
                   url.Contains("scholar.google");
        }
            
            // STEP 1: Always try direct access first to save ScraperAPI tokens
        
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
            
            Console.WriteLine("[DEBUG] Starting arXiv-specific metadata extraction");

            // Extract title - try multiple selectors
            var titleSelectors = new[]
            {
                "//meta[@property='og:title']",
                "//meta[@name='citation_title']",
                "//h1[@class='title mathjax']",
                "//h1[contains(@class, 'title')]",
                "//title"
            };

            foreach (var selector in titleSelectors)
            {
                var titleElement = doc.DocumentNode.SelectSingleNode(selector);
                if (titleElement != null)
                {
                    var title = titleElement.Name == "meta" 
                        ? GetAttributeValue(titleElement, "content") 
                        : titleElement.InnerText?.Trim();
                    
                    if (!string.IsNullOrEmpty(title) && title.Length > 5)
                    {
                        metadata.Title = title.Replace("Title:", "").Replace("[arXiv:", "").Trim();
                        Console.WriteLine($"[DEBUG] arXiv title extracted via {selector}: {metadata.Title}");
                        break;
                    }
                }
            }

            // Extract authors - try citation meta first, then DOM
            var authorElements = doc.DocumentNode.SelectNodes("//meta[@name='citation_author']");
            if (authorElements != null && authorElements.Any())
            {
                var authors = authorElements.Select(e => GetAttributeValue(e, "content"))
                                          .Where(a => !string.IsNullOrEmpty(a))
                                          .ToList();
                if (authors.Any())
                {
                    metadata.Authors = string.Join(", ", authors);
                    metadata.Author = authors.First();
                    Console.WriteLine($"[DEBUG] arXiv authors via citation meta: {metadata.Authors}");
                }
            }
            else
            {
                // Fallback to DOM selectors
                var authorsElement = doc.DocumentNode.SelectSingleNode("//div[@class='authors']");
                if (authorsElement != null)
                {
                    var authorLinks = authorsElement.SelectNodes(".//a");
                    if (authorLinks != null)
                    {
                        var authors = authorLinks.Select(a => a.InnerText?.Trim())
                                                .Where(a => !string.IsNullOrEmpty(a))
                                                .ToList();
                        if (authors.Any())
                        {
                            metadata.Authors = string.Join(", ", authors);
                            metadata.Author = authors.First();
                            Console.WriteLine($"[DEBUG] arXiv authors via DOM: {metadata.Authors}");
                        }
                    }
                }
            }

            // Extract abstract - multiple approaches
            var abstractSelectors = new[]
            {
                "//meta[@name='citation_abstract']",
                "//meta[@property='og:description']",
                "//blockquote[@class='abstract mathjax']",
                "//blockquote[contains(@class, 'abstract')]",
                "//div[@class='abstract']"
            };

            foreach (var selector in abstractSelectors)
            {
                var abstractElement = doc.DocumentNode.SelectSingleNode(selector);
                if (abstractElement != null)
                {
                    var abstractText = abstractElement.Name == "meta" 
                        ? GetAttributeValue(abstractElement, "content")
                        : abstractElement.InnerText?.Trim();
                    
                    if (!string.IsNullOrEmpty(abstractText) && abstractText.Length > 50)
                    {
                        metadata.Abstract = abstractText.Replace("Abstract:", "").Trim();
                        metadata.Description = metadata.Abstract;
                        Console.WriteLine($"[DEBUG] arXiv abstract found via {selector}: {metadata.Abstract.Substring(0, Math.Min(100, metadata.Abstract.Length))}...");
                        break;
                    }
                }
            }

            // Extract arXiv ID and set DOI-like identifier
            var arxivMatch = System.Text.RegularExpressions.Regex.Match(url, @"abs/(\d+\.\d+)");
            if (arxivMatch.Success)
            {
                metadata.DOI = $"arXiv:{arxivMatch.Groups[1].Value}";
                Console.WriteLine($"[DEBUG] arXiv ID extracted: {metadata.DOI}");
            }

            // Extract subject/category
            var subjectElement = doc.DocumentNode.SelectSingleNode("//td[@class='tablecell subjects']") ??
                               doc.DocumentNode.SelectSingleNode("//span[@class='primary-subject']");
            if (subjectElement != null)
            {
                metadata.Subject = subjectElement.InnerText?.Trim();
                Console.WriteLine($"[DEBUG] arXiv subject: {metadata.Subject}");
            }

            metadata.Publisher = "arXiv";
            metadata.Language = "en";
            metadata.RetrievedDate = DateTime.UtcNow;
            
            Console.WriteLine("[DEBUG] arXiv metadata extraction completed");
            return metadata;
        }

        /// <summary>
        /// Extracts metadata from PubMed articles.
        /// </summary>
        private DocumentMetadataDto ExtractPubMedMetadata(HtmlDocument doc, string url)
        {
            var metadata = new DocumentMetadataDto { URL = url, Source = "PubMed" };
            
            Console.WriteLine("[DEBUG] Starting PubMed-specific metadata extraction");

            // Extract title - prioritize OpenGraph and citation meta
            var titleSelectors = new[]
            {
                "//meta[@property='og:title']",
                "//meta[@name='citation_title']",
                "//h1[@class='heading-title']",
                "//h1[contains(@class, 'title')]",
                "//title"
            };

            foreach (var selector in titleSelectors)
            {
                var titleElement = doc.DocumentNode.SelectSingleNode(selector);
                if (titleElement != null)
                {
                    var title = titleElement.Name == "meta" 
                        ? GetAttributeValue(titleElement, "content") 
                        : titleElement.InnerText?.Trim();
                    
                    if (!string.IsNullOrEmpty(title) && title.Length > 5)
                    {
                        // Clean up PubMed-specific suffixes
                        metadata.Title = title.Replace("- PubMed", "").Replace("PubMed", "").Trim();
                        Console.WriteLine($"[DEBUG] PubMed title extracted via {selector}: {metadata.Title}");
                        break;
                    }
                }
            }

            // Extract authors - try citation meta first, then DOM
            var authorElements = doc.DocumentNode.SelectNodes("//meta[@name='citation_author']");
            if (authorElements != null && authorElements.Any())
            {
                var authors = authorElements.Select(e => GetAttributeValue(e, "content"))
                                          .Where(a => !string.IsNullOrEmpty(a))
                                          .ToList();
                if (authors.Any())
                {
                    metadata.Authors = string.Join(", ", authors);
                    metadata.Author = authors.First();
                    Console.WriteLine($"[DEBUG] PubMed authors via citation meta: {metadata.Authors}");
                }
            }
            else
            {
                // Fallback to DOM selectors for authors
                var authorSelectors = new[]
                {
                    "//div[@class='authors-list']//a[@class='full-name']",
                    "//div[contains(@class, 'authors')]//span[contains(@class, 'authors-list-item')]",
                    "//div[@class='auths']//a"
                };

                var authors = new List<string>();
                foreach (var selector in authorSelectors)
                {
                    var authorElems = doc.DocumentNode.SelectNodes(selector);
                    if (authorElems != null)
                    {
                        foreach (var elem in authorElems)
                        {
                            var author = elem.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(author) && author.Length > 2)
                            {
                                authors.Add(author);
                            }
                        }
                        if (authors.Any()) break;
                    }
                }

                if (authors.Any())
                {
                    metadata.Authors = string.Join(", ", authors);
                    metadata.Author = authors.First();
                    Console.WriteLine($"[DEBUG] PubMed authors via DOM: {metadata.Authors}");
                }
            }

            // Extract journal
            var journalSelectors = new[]
            {
                "//meta[@name='citation_journal_title']",
                "//meta[@name='citation_journal_abbrev']",
                "//button[@id='full-view-journal-trigger']"
            };

            foreach (var selector in journalSelectors)
            {
                var journalElement = doc.DocumentNode.SelectSingleNode(selector);
                if (journalElement != null)
                {
                    var journal = journalElement.Name == "meta" 
                        ? GetAttributeValue(journalElement, "content")
                        : journalElement.InnerText?.Trim();
                    
                    if (!string.IsNullOrEmpty(journal))
                    {
                        metadata.Journal = journal;
                        Console.WriteLine($"[DEBUG] PubMed journal via {selector}: {metadata.Journal}");
                        break;
                    }
                }
            }

            // Extract DOI
            var doiElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_doi']");
            if (doiElement != null)
            {
                metadata.DOI = GetAttributeValue(doiElement, "content");
                Console.WriteLine($"[DEBUG] PubMed DOI: {metadata.DOI}");
            }

            // Extract publication date
            var dateSelectors = new[]
            {
                "//meta[@name='citation_publication_date']",
                "//meta[@name='citation_date']",
                "//span[@class='cit']"
            };

            foreach (var selector in dateSelectors)
            {
                var dateElement = doc.DocumentNode.SelectSingleNode(selector);
                if (dateElement != null)
                {
                    var dateStr = dateElement.Name == "meta" 
                        ? GetAttributeValue(dateElement, "content")
                        : dateElement.InnerText?.Trim();
                    
                    if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var pubDate))
                    {
                        metadata.PublicationDate = DateOnly.FromDateTime(pubDate);
                        Console.WriteLine($"[DEBUG] PubMed date via {selector}: {dateStr}");
                        break;
                    }
                }
            }

            // Extract abstract - multiple approaches
            var abstractSelectors = new[]
            {
                "//meta[@property='og:description']",
                "//meta[@name='citation_abstract']",
                "//meta[@name='description']",
                "//div[@class='abstract-content selected']",
                "//div[@id='enc-abstract']//p",
                "//div[contains(@class, 'abstract')]"
            };

            foreach (var selector in abstractSelectors)
            {
                var abstractElement = doc.DocumentNode.SelectSingleNode(selector);
                if (abstractElement != null)
                {
                    var abstractText = abstractElement.Name == "meta" 
                        ? GetAttributeValue(abstractElement, "content")
                        : abstractElement.InnerText?.Trim();
                    
                    if (!string.IsNullOrEmpty(abstractText) && abstractText.Length > 50)
                    {
                        // Clean up PubMed-specific suffixes
                        abstractText = abstractText.Replace("PubMed", "").Trim();
                        metadata.Abstract = abstractText;
                        metadata.Description = abstractText;
                        Console.WriteLine($"[DEBUG] PubMed abstract found via {selector}: {abstractText.Substring(0, Math.Min(100, abstractText.Length))}...");
                        break;
                    }
                }
            }

            metadata.Publisher = "PubMed";
            metadata.Language = "en";
            metadata.RetrievedDate = DateTime.UtcNow;
            
            Console.WriteLine("[DEBUG] PubMed metadata extraction completed");
            return metadata;
        }

        /// <summary>
        /// Extracts metadata from IEEE Xplore articles.
        /// </summary>
        private DocumentMetadataDto ExtractIEEEMetadata(HtmlDocument doc, string url)
        {
            var metadata = new DocumentMetadataDto { URL = url, Source = "IEEE Xplore" };
            
            Console.WriteLine("[DEBUG] Starting IEEE-specific metadata extraction");

            // Extract title - prioritize OpenGraph and citation meta
            var titleSelectors = new[]
            {
                "//meta[@property='og:title']",
                "//meta[@name='citation_title']",
                "//h1[@class='document-title']",
                "//h1[contains(@class, 'title')]",
                "//title"
            };

            foreach (var selector in titleSelectors)
            {
                var titleElement = doc.DocumentNode.SelectSingleNode(selector);
                if (titleElement != null)
                {
                    var title = titleElement.Name == "meta" 
                        ? GetAttributeValue(titleElement, "content") 
                        : titleElement.InnerText?.Trim();
                    
                    if (!string.IsNullOrEmpty(title) && title.Length > 5)
                    {
                        // Clean up IEEE-specific prefixes
                        metadata.Title = title.Replace("IEEE Xplore", "").Replace("| IEEE", "").Trim();
                        Console.WriteLine($"[DEBUG] IEEE title extracted via {selector}: {metadata.Title}");
                        break;
                    }
                }
            }

            // Extract authors - try citation meta first, then DOM
            var authorElements = doc.DocumentNode.SelectNodes("//meta[@name='citation_author']");
            if (authorElements != null && authorElements.Any())
            {
                var authors = authorElements.Select(e => GetAttributeValue(e, "content"))
                                          .Where(a => !string.IsNullOrEmpty(a))
                                          .ToList();
                if (authors.Any())
                {
                    metadata.Authors = string.Join(", ", authors);
                    metadata.Author = authors.First();
                    Console.WriteLine($"[DEBUG] IEEE authors via citation meta: {metadata.Authors}");
                }
            }
            else
            {
                // Fallback to DOM selectors for authors
                var authorSelectors = new[]
                {
                    "//div[contains(@class, 'authors')]//span[contains(@class, 'author-name')]",
                    "//div[@class='authors-info']//a",
                    "//span[@class='authors-list']//a"
                };

                var authors = new List<string>();
                foreach (var selector in authorSelectors)
                {
                    var authorElems = doc.DocumentNode.SelectNodes(selector);
                    if (authorElems != null)
                    {
                        foreach (var elem in authorElems)
                        {
                            var author = elem.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(author) && author.Length > 2)
                            {
                                authors.Add(author);
                            }
                        }
                        if (authors.Any()) break;
                    }
                }

                if (authors.Any())
                {
                    metadata.Authors = string.Join(", ", authors);
                    metadata.Author = authors.First();
                    Console.WriteLine($"[DEBUG] IEEE authors via DOM: {metadata.Authors}");
                }
            }

            // Extract conference/journal
            var venueSelectors = new[]
            {
                "//meta[@name='citation_conference_title']",
                "//meta[@name='citation_journal_title']",
                "//meta[@property='og:site_name']"
            };

            foreach (var selector in venueSelectors)
            {
                var venueElement = doc.DocumentNode.SelectSingleNode(selector);
                if (venueElement != null)
                {
                    var venue = GetAttributeValue(venueElement, "content");
                    if (!string.IsNullOrEmpty(venue))
                    {
                        metadata.Journal = venue;
                        Console.WriteLine($"[DEBUG] IEEE venue via {selector}: {metadata.Journal}");
                        break;
                    }
                }
            }

            // Extract DOI
            var doiElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_doi']");
            if (doiElement != null)
            {
                metadata.DOI = GetAttributeValue(doiElement, "content");
                Console.WriteLine($"[DEBUG] IEEE DOI: {metadata.DOI}");
            }

            // Extract publication date
            var dateSelectors = new[]
            {
                "//meta[@name='citation_publication_date']",
                "//meta[@name='citation_date']",
                "//meta[@name='citation_online_date']"
            };

            foreach (var selector in dateSelectors)
            {
                var dateElement = doc.DocumentNode.SelectSingleNode(selector);
                if (dateElement != null)
                {
                    var dateStr = GetAttributeValue(dateElement, "content");
                    if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var pubDate))
                    {
                        metadata.PublicationDate = DateOnly.FromDateTime(pubDate);
                        Console.WriteLine($"[DEBUG] IEEE date via {selector}: {dateStr}");
                        break;
                    }
                }
            }

            // Extract abstract - multiple approaches
            var abstractSelectors = new[]
            {
                "//meta[@property='og:description']",
                "//meta[@name='citation_abstract']",
                "//meta[@name='description']",
                "//div[@class='abstract-text']",
                "//div[contains(@class, 'abstract')]"
            };

            foreach (var selector in abstractSelectors)
            {
                var abstractElement = doc.DocumentNode.SelectSingleNode(selector);
                if (abstractElement != null)
                {
                    var abstractText = abstractElement.Name == "meta" 
                        ? GetAttributeValue(abstractElement, "content")
                        : abstractElement.InnerText?.Trim();
                    
                    if (!string.IsNullOrEmpty(abstractText) && abstractText.Length > 50)
                    {
                        // Clean up IEEE-specific suffixes
                        abstractText = abstractText.Replace("IEEE Xplore", "").Trim();
                        metadata.Abstract = abstractText;
                        metadata.Description = abstractText;
                        Console.WriteLine($"[DEBUG] IEEE abstract found via {selector}: {abstractText.Substring(0, Math.Min(100, abstractText.Length))}...");
                        break;
                    }
                }
            }

            metadata.Publisher = "IEEE";
            metadata.Language = "en";
            metadata.RetrievedDate = DateTime.UtcNow;
            
            Console.WriteLine("[DEBUG] IEEE metadata extraction completed");
            return metadata;
        }

        /// <summary>
        /// Extracts metadata from Springer articles.
        /// </summary>
        private DocumentMetadataDto ExtractSpringerMetadata(HtmlDocument doc, string url)
        {
            var metadata = new DocumentMetadataDto { URL = url, Source = "Springer" };
            
            Console.WriteLine("[DEBUG] Starting Springer-specific metadata extraction");

            // Extract title - prioritize OpenGraph and citation meta
            var titleSelectors = new[]
            {
                "//meta[@property='og:title']",
                "//meta[@name='citation_title']",
                "//h1[@class='c-article-title']",
                "//h1[contains(@class, 'title')]",
                "//title"
            };

            foreach (var selector in titleSelectors)
            {
                var titleElement = doc.DocumentNode.SelectSingleNode(selector);
                if (titleElement != null)
                {
                    var title = titleElement.Name == "meta" 
                        ? GetAttributeValue(titleElement, "content") 
                        : titleElement.InnerText?.Trim();
                    
                    if (!string.IsNullOrEmpty(title) && title.Length > 5)
                    {
                        // Clean up Springer-specific prefixes
                        metadata.Title = title.Replace("| SpringerLink", "").Replace("Springer", "").Trim();
                        Console.WriteLine($"[DEBUG] Springer title extracted via {selector}: {metadata.Title}");
                        break;
                    }
                }
            }

            // Extract authors - try citation meta first, then DOM
            var authorElements = doc.DocumentNode.SelectNodes("//meta[@name='citation_author']");
            if (authorElements != null && authorElements.Any())
            {
                var authors = authorElements.Select(e => GetAttributeValue(e, "content"))
                                          .Where(a => !string.IsNullOrEmpty(a))
                                          .ToList();
                if (authors.Any())
                {
                    metadata.Authors = string.Join(", ", authors);
                    metadata.Author = authors.First();
                    Console.WriteLine($"[DEBUG] Springer authors via citation meta: {metadata.Authors}");
                }
            }
            else
            {
                // Fallback to DOM selectors for authors
                var authorSelectors = new[]
                {
                    "//div[contains(@class, 'c-article-authors')]//a[contains(@class, 'c-author-name')]",
                    "//ol[contains(@class, 'c-article-author-list')]//span[@class='c-article-author-name']",
                    "//div[@class='authors-list']//a"
                };

                var authors = new List<string>();
                foreach (var selector in authorSelectors)
                {
                    var authorElems = doc.DocumentNode.SelectNodes(selector);
                    if (authorElems != null)
                    {
                        foreach (var elem in authorElems)
                        {
                            var author = elem.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(author) && author.Length > 2)
                            {
                                authors.Add(author);
                            }
                        }
                        if (authors.Any()) break;
                    }
                }

                if (authors.Any())
                {
                    metadata.Authors = string.Join(", ", authors);
                    metadata.Author = authors.First();
                    Console.WriteLine($"[DEBUG] Springer authors via DOM: {metadata.Authors}");
                }
            }

            // Extract journal
            var journalSelectors = new[]
            {
                "//meta[@name='citation_journal_title']",
                "//meta[@property='og:site_name']",
                "//span[@class='JournalTitle']"
            };

            foreach (var selector in journalSelectors)
            {
                var journalElement = doc.DocumentNode.SelectSingleNode(selector);
                if (journalElement != null)
                {
                    var journal = journalElement.Name == "meta" 
                        ? GetAttributeValue(journalElement, "content")
                        : journalElement.InnerText?.Trim();
                    
                    if (!string.IsNullOrEmpty(journal))
                    {
                        metadata.Journal = journal;
                        Console.WriteLine($"[DEBUG] Springer journal via {selector}: {metadata.Journal}");
                        break;
                    }
                }
            }

            // Extract DOI
            var doiElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_doi']");
            if (doiElement != null)
            {
                metadata.DOI = GetAttributeValue(doiElement, "content");
                Console.WriteLine($"[DEBUG] Springer DOI: {metadata.DOI}");
            }

            // Extract publication date
            var dateSelectors = new[]
            {
                "//meta[@name='citation_publication_date']",
                "//meta[@name='citation_date']",
                "//meta[@name='citation_online_date']"
            };

            foreach (var selector in dateSelectors)
            {
                var dateElement = doc.DocumentNode.SelectSingleNode(selector);
                if (dateElement != null)
                {
                    var dateStr = GetAttributeValue(dateElement, "content");
                    if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var pubDate))
                    {
                        metadata.PublicationDate = DateOnly.FromDateTime(pubDate);
                        Console.WriteLine($"[DEBUG] Springer date via {selector}: {dateStr}");
                        break;
                    }
                }
            }

            // Extract abstract - multiple approaches
            var abstractSelectors = new[]
            {
                "//meta[@property='og:description']",
                "//meta[@name='citation_abstract']",
                "//meta[@name='description']",
                "//div[@class='c-article__section']//div[@id='Abs1-content']",
                "//section[@class='Abstract']//p",
                "//div[contains(@class, 'abstract')]"
            };

            foreach (var selector in abstractSelectors)
            {
                var abstractElement = doc.DocumentNode.SelectSingleNode(selector);
                if (abstractElement != null)
                {
                    var abstractText = abstractElement.Name == "meta" 
                        ? GetAttributeValue(abstractElement, "content")
                        : abstractElement.InnerText?.Trim();
                    
                    if (!string.IsNullOrEmpty(abstractText) && abstractText.Length > 50)
                    {
                        // Clean up Springer-specific suffixes
                        abstractText = abstractText.Replace("SpringerLink", "").Trim();
                        metadata.Abstract = abstractText;
                        metadata.Description = abstractText;
                        Console.WriteLine($"[DEBUG] Springer abstract found via {selector}: {abstractText.Substring(0, Math.Min(100, abstractText.Length))}...");
                        break;
                    }
                }
            }

            metadata.Publisher = "Springer";
            metadata.Language = "en";
            metadata.RetrievedDate = DateTime.UtcNow;
            
            Console.WriteLine("[DEBUG] Springer metadata extraction completed");
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

            // Determine source from URL
            var uri = new Uri(url);
            var domain = uri.Host.ToLowerInvariant();
            metadata.Source = domain.Replace("www.", "").Split('.')[0];

            // Enhanced title extraction with multiple strategies
            var titleSelectors = new[]
            {
                "//meta[@property='og:title']",
                "//meta[@name='twitter:title']", 
                "//meta[@name='citation_title']",
                "//title",
                "//h1[contains(@class, 'title')]",
                "//h1[@class='entry-title']",
                "//h1[@class='article-title']",
                "//h1"
            };

            Console.WriteLine("[DEBUG] Extracting title using enhanced selectors...");
            foreach (var selector in titleSelectors)
            {
                var titleElement = doc.DocumentNode.SelectSingleNode(selector);
                Console.WriteLine($"[DEBUG] Title selector '{selector}' found: {titleElement != null}");
                
                if (titleElement != null)
                {
                    var title = titleElement.Name == "meta" 
                        ? GetAttributeValue(titleElement, "content") 
                        : titleElement.InnerText?.Trim();
                    
                    Console.WriteLine($"[DEBUG] Extracted title: '{title}' (length: {title?.Length ?? 0})");
                    if (!string.IsNullOrEmpty(title) && title.Length > 5)
                    {
                        // Clean up common website suffixes
                        title = CleanTitle(title, domain);
                        metadata.Title = title;
                        Console.WriteLine($"[DEBUG] Title accepted: {title}");
                        break;
                    }
                }
            }

            // Enhanced description/abstract extraction
            Console.WriteLine("[DEBUG] Extracting description...");
            var descriptionSelectors = new[]
            {
                "//meta[@property='og:description']",
                "//meta[@name='description']",
                "//meta[@name='twitter:description']",
                "//meta[@name='citation_abstract']",
                "//div[contains(@class, 'abstract')]",
                "//div[contains(@class, 'summary')]",
                "//div[contains(@class, 'excerpt')]",
                "//p[contains(@class, 'description')]"
            };

            foreach (var selector in descriptionSelectors)
            {
                var descElement = doc.DocumentNode.SelectSingleNode(selector);
                Console.WriteLine($"[DEBUG] Description selector '{selector}' found: {descElement != null}");
                
                if (descElement != null)
                {
                    var description = descElement.Name == "meta" 
                        ? GetAttributeValue(descElement, "content") 
                        : descElement.InnerText?.Trim();
                    
                    Console.WriteLine($"[DEBUG] Extracted description: '{description?.Substring(0, Math.Min(100, description?.Length ?? 0))}...' (length: {description?.Length ?? 0})");
                    if (!string.IsNullOrEmpty(description) && description.Length > 30)
                    {
                        metadata.Description = description;
                        metadata.Abstract = description;
                        Console.WriteLine($"[DEBUG] Description accepted (length: {description.Length})");
                        break;
                    }
                }
            }

            // Enhanced author extraction
            Console.WriteLine("[DEBUG] Extracting authors...");
            var authorElements = doc.DocumentNode.SelectNodes("//meta[@name='citation_author']");
            if (authorElements != null && authorElements.Any())
            {
                var authors = authorElements.Select(e => GetAttributeValue(e, "content"))
                                          .Where(a => !string.IsNullOrEmpty(a))
                                          .ToList();
                if (authors.Any())
                {
                    metadata.Authors = string.Join(", ", authors);
                    metadata.Author = authors.First();
                    Console.WriteLine($"[DEBUG] Authors via citation meta: {metadata.Authors}");
                }
            }
            else
            {
                // Fallback to other author selectors
                var authorSelectors = new[]
                {
                    "//meta[@name='author']",
                    "//meta[@property='article:author']",
                    "//span[contains(@class, 'author')]",
                    "//div[contains(@class, 'author')]",
                    "//a[contains(@class, 'author')]"
                };

                var authors = new List<string>();
                foreach (var selector in authorSelectors)
                {
                    var authorElems = doc.DocumentNode.SelectNodes(selector);
                    if (authorElems != null)
                    {
                        foreach (var elem in authorElems)
                        {
                            var author = elem.Name == "meta" 
                                ? GetAttributeValue(elem, "content")
                                : elem.InnerText?.Trim();
                            
                            if (!string.IsNullOrEmpty(author) && author.Length > 2 && author.Length < 100)
                            {
                                authors.Add(author);
                            }
                        }
                        if (authors.Any()) break;
                    }
                }

                if (authors.Any())
                {
                    metadata.Authors = string.Join(", ", authors.Distinct());
                    metadata.Author = authors.First();
                    Console.WriteLine($"[DEBUG] Authors via DOM: {metadata.Authors}");
                }
            }

            // Enhanced publication date extraction
            Console.WriteLine("[DEBUG] Extracting publication date...");
            var dateSelectors = new[]
            {
                "//meta[@name='citation_publication_date']",
                "//meta[@name='citation_date']",
                "//meta[@property='article:published_time']",
                "//meta[@name='date']",
                "//time[@class='published']",
                "//span[contains(@class, 'date')]"
            };

            foreach (var selector in dateSelectors)
            {
                var dateElement = doc.DocumentNode.SelectSingleNode(selector);
                if (dateElement != null)
                {
                    var dateStr = dateElement.Name == "meta" 
                        ? GetAttributeValue(dateElement, "content")
                        : dateElement.GetAttributeValue("datetime", "") ?? dateElement.InnerText?.Trim();
                    
                    if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var pubDate))
                    {
                        metadata.PublicationDate = DateOnly.FromDateTime(pubDate);
                        Console.WriteLine($"[DEBUG] Publication date via {selector}: {dateStr}");
                        break;
                    }
                }
            }

            // Enhanced journal/publisher extraction
            Console.WriteLine("[DEBUG] Extracting journal/publisher...");
            var publisherSelectors = new[]
            {
                "//meta[@name='citation_journal_title']",
                "//meta[@name='citation_publisher']",
                "//meta[@property='og:site_name']",
                "//meta[@name='publisher']"
            };

            foreach (var selector in publisherSelectors)
            {
                var pubElement = doc.DocumentNode.SelectSingleNode(selector);
                if (pubElement != null)
                {
                    var publisher = GetAttributeValue(pubElement, "content");
                    if (!string.IsNullOrEmpty(publisher))
                    {
                        if (selector.Contains("journal"))
                        {
                            metadata.Journal = publisher;
                        }
                        else
                        {
                            metadata.Publisher = publisher;
                        }
                        Console.WriteLine($"[DEBUG] Publisher/Journal via {selector}: {publisher}");
                        break;
                    }
                }
            }

            // DOI extraction
            var doiElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_doi']");
            if (doiElement != null)
            {
                metadata.DOI = GetAttributeValue(doiElement, "content");
                Console.WriteLine($"[DEBUG] DOI found: {metadata.DOI}");
            }

            // Set language (default to English for most academic content)
            metadata.Language = "en";
            metadata.RetrievedDate = DateTime.UtcNow;

            // If no source was set, use the domain
            if (string.IsNullOrEmpty(metadata.Source))
            {
                metadata.Source = domain;
            }

            Console.WriteLine($"[DEBUG] ExtractStandardWebMetadata completed. Final metadata:");
            Console.WriteLine($"[DEBUG] - Title: {metadata.Title}");
            Console.WriteLine($"[DEBUG] - Authors: {metadata.Authors}");
            Console.WriteLine($"[DEBUG] - Description length: {metadata.Description?.Length ?? 0}");
            Console.WriteLine($"[DEBUG] - Source: {metadata.Source}");

            return metadata;
        }

        /// <summary>
        /// Cleans title by removing common website suffixes and formatting.
        /// </summary>
        private string CleanTitle(string title, string domain)
        {
            if (string.IsNullOrEmpty(title)) return title;

            // Common suffixes to remove
            var suffixesToRemove = new[]
            {
                $" | {domain}",
                $" - {domain}",
                " | Home",
                " - Home",
                " | Homepage",
                " - Homepage"
            };

            foreach (var suffix in suffixesToRemove)
            {
                if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    title = title.Substring(0, title.Length - suffix.Length);
                }
            }

            return title.Trim();
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