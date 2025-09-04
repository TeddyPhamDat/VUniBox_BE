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

                // Extract volume and issue
                var volumeElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_volume']");
                if (volumeElement != null)
                {
                    metadata.Volume = GetAttributeValue(volumeElement, "content");
                }

                var issueElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_issue']");
                if (issueElement != null)
                {
                    metadata.Issue = GetAttributeValue(issueElement, "content");
                }

                // Extract pages
                var pagesElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_firstpage']");
                if (pagesElement != null)
                {
                    var firstPage = GetAttributeValue(pagesElement, "content");
                    var lastPageElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_lastpage']");
                    var lastPage = lastPageElement != null ? GetAttributeValue(lastPageElement, "content") : null;
                    
                    metadata.Pages = lastPage != null && firstPage != lastPage ? $"{firstPage}-{lastPage}" : firstPage;
                }

                // Extract abstract
                var abstractSelectors = new[]
                {
                    "meta[name='citation_abstract']",
                    "meta[property='og:description']",
                    ".abstract",
                    ".summary"
                };

                foreach (var selector in abstractSelectors)
                {
                    var abstractElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (abstractElement != null)
                    {
                        metadata.Abstract = GetAttributeValue(abstractElement, "content") ?? abstractElement.InnerText?.Trim();
                        break;
                    }
                }

                // Extract keywords
                var keywordsElement = doc.DocumentNode.SelectSingleNode("//meta[@name='citation_keywords']");
                if (keywordsElement != null)
                {
                    metadata.Keywords = GetAttributeValue(keywordsElement, "content");
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

                // Extract ISBN
                var isbnMatch = Regex.Match(html, @"ISBN[:\s]*([0-9\-X]+)", RegexOptions.IgnoreCase);
                if (isbnMatch.Success)
                {
                    metadata.ISBN = isbnMatch.Groups[1].Value;
                }

                // Extract publisher
                var publisherSelectors = new[]
                {
                    "meta[name='citation_publisher']",
                    "meta[property='book:publisher']",
                    ".publisher",
                    ".imprint"
                };

                foreach (var selector in publisherSelectors)
                {
                    var publisherElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (publisherElement != null)
                    {
                        metadata.Publisher = GetAttributeValue(publisherElement, "content") ?? publisherElement.InnerText?.Trim();
                        break;
                    }
                }

                // Extract publication date
                var dateSelectors = new[]
                {
                    "meta[name='citation_publication_date']",
                    "meta[property='book:release_date']",
                    ".publication-date",
                    ".publish-date"
                };

                foreach (var selector in dateSelectors)
                {
                    var dateElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (dateElement != null)
                    {
                        var dateStr = GetAttributeValue(dateElement, "content") ?? dateElement.InnerText?.Trim();
                        if (DateOnly.TryParse(dateStr, out var date))
                        {
                            metadata.PublicationDate = date;
                        }
                        break;
                    }
                }
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
                    "meta[property='og:article:published_time']",
                    ".publish-date",
                    ".article-date",
                    "time[datetime]"
                };

                foreach (var selector in dateSelectors)
                {
                    var dateElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (dateElement != null)
                    {
                        var dateStr = GetAttributeValue(dateElement, "content") ?? 
                                     GetAttributeValue(dateElement, "datetime") ?? 
                                     dateElement.InnerText?.Trim();
                        
                        if (DateOnly.TryParse(dateStr, out var date))
                        {
                            metadata.PublicationDate = date;
                        }
                        break;
                    }
                }

                // Extract author
                var authorSelectors = new[]
                {
                    "meta[name='article:author']",
                    "meta[property='article:author']",
                    "meta[name='author']",
                    ".author",
                    ".byline",
                    ".writer"
                };

                foreach (var selector in authorSelectors)
                {
                    var authorElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (authorElement != null)
                    {
                        metadata.Author = GetAttributeValue(authorElement, "content") ?? authorElement.InnerText?.Trim();
                        break;
                    }
                }
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
                    "title"
                };

                foreach (var selector in titleSelectors)
                {
                    var titleElement = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (titleElement != null)
                    {
                        metadata.Title = GetAttributeValue(titleElement, "content") ?? titleElement.InnerText?.Trim();
                        break;
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
                        metadata.Description = GetAttributeValue(descElement, "content") ?? descElement.InnerText?.Trim();
                        break;
                    }
                }

                // Extract site name
                var siteElement = doc.DocumentNode.SelectSingleNode("//meta[@property='og:site_name']");
                if (siteElement != null)
                {
                    metadata.Source = GetAttributeValue(siteElement, "content");
                }

                // Extract author
                var authorElement = doc.DocumentNode.SelectSingleNode("//meta[@name='author']");
                if (authorElement != null)
                {
                    metadata.Author = GetAttributeValue(authorElement, "content");
                }

                // Extract publication date
                var dateElement = doc.DocumentNode.SelectSingleNode("//meta[@property='article:published_time']");
                if (dateElement != null)
                {
                    var dateStr = GetAttributeValue(dateElement, "content");
                    if (DateOnly.TryParse(dateStr, out var date))
                    {
                        metadata.PublicationDate = date;
                    }
                }
            }
            catch (Exception ex)
            {
                metadata.Title = "Error extracting metadata";
                metadata.Description = $"Failed to extract metadata: {ex.Message}";
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
