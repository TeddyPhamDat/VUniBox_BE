using System;
using VUniBox.Models.DTO;

namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Enhanced metadata DTO specifically for citation generation
    /// Contains complete document metadata for all citation styles
    /// </summary>
    public class CitationMetadataDto
    {
        // Document identification
        public int DocumentId { get; set; }
        public string Type { get; set; } = string.Empty; // Document type
        public string Status { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        
        // Core citation fields - REQUIRED for all citation styles
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Authors { get; set; } = string.Empty; // Full author list
        public DateOnly? PublicationDate { get; set; }
        
        // Publication details
        public string Publisher { get; set; } = string.Empty;
        public string Journal { get; set; } = string.Empty;
        public string Volume { get; set; } = string.Empty;
        public string Issue { get; set; } = string.Empty;
        public string Pages { get; set; } = string.Empty;
        
        // Identifiers
        public string Doi { get; set; } = string.Empty;
        public string Isbn { get; set; } = string.Empty;
        public string URL { get; set; } = string.Empty;
        
        // Content description
        public string Abstract { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        
        // Source information
        public string Source { get; set; } = string.Empty; // Website or database name
        public DateTime? RetrievedDate { get; set; }
        
        // File information (if applicable)
        public string FilePath { get; set; } = string.Empty;
        public long? FileSize { get; set; }
        public string FileType { get; set; } = string.Empty;
        
        // Constructor from Documents entity and extracted metadata
        public CitationMetadataDto() { }
        
        public CitationMetadataDto(Documents document, DocumentMetadataDto? extractedMetadata)
        {
            // Document info
            DocumentId = document.DocumentId;
            Type = document.DocumentType.ToString();
            Status = document.Status ?? string.Empty;
            CreatedAt = document.CreatedAt;
            
            // Core citation fields - prefer extracted metadata, fallback to document, then to smart defaults
            Title = GetSafeValue(extractedMetadata?.Title, document.Title, "Untitled Document");
            Author = GetSafeValue(extractedMetadata?.Author, document.Author, GenerateSmartAuthor(document.Title, document.SourceUrl));
            Authors = GetSafeValue(extractedMetadata?.Authors, document.Authors, Author);
            PublicationDate = extractedMetadata?.PublicationDate ?? document.PublicationDate;
            
            // Publication details
            Publisher = GetSafeValue(extractedMetadata?.Publisher, document.Publisher, GenerateSmartPublisher(document.SourceUrl));
            Journal = GetSafeValue(extractedMetadata?.Journal, document.Journal, string.Empty);
            Volume = GetSafeValue(extractedMetadata?.Volume, document.Volume, string.Empty);
            Issue = GetSafeValue(extractedMetadata?.Issue, document.Issue, string.Empty);
            Pages = GetSafeValue(extractedMetadata?.Pages, document.Pages, string.Empty);
            
            // Identifiers
            Doi = GetSafeValue(extractedMetadata?.DOI, document.Doi, string.Empty);
            Isbn = GetSafeValue(extractedMetadata?.ISBN, document.Isbn, string.Empty);
            URL = GetSafeValue(extractedMetadata?.URL, document.SourceUrl, string.Empty);
            
            // Content
            Abstract = GetSafeValue(extractedMetadata?.Abstract, document.Abstract, string.Empty);
            Keywords = GetSafeValue(extractedMetadata?.Keywords, document.Keywords, string.Empty);
            Subject = GetSafeValue(extractedMetadata?.Subject, document.Subject, string.Empty);
            Language = GetSafeValue(extractedMetadata?.Language, document.Language, "English");
            
            // Source info
            Source = GetSafeValue(extractedMetadata?.Source, document.Source, GenerateSmartSource(document.SourceUrl));
            RetrievedDate = extractedMetadata?.RetrievedDate ?? document.RetrievedDate;
            
            // File info
            FilePath = GetSafeValue(extractedMetadata?.FilePath, document.FilePath, string.Empty);
            FileSize = extractedMetadata?.FileSize ?? 0;
            FileType = GetSafeValue(extractedMetadata?.FileType, null, string.Empty);
        }



        private static string GetSafeValue(string? primary, string? secondary, string fallback)
        {
            return !string.IsNullOrWhiteSpace(primary) ? primary : 
                   !string.IsNullOrWhiteSpace(secondary) ? secondary : fallback;
        }

        private static string GenerateSmartAuthor(string? title, string? sourceUrl)
        {
            if (!string.IsNullOrWhiteSpace(sourceUrl))
            {
                var uri = new Uri(sourceUrl);
                var domain = uri.Host.ToLower();
                
                if (domain.Contains("wikipedia"))
                    return "Wikipedia Contributors";
                if (domain.Contains("researchgate"))
                    return "ResearchGate Researcher";
                if (domain.Contains("arxiv"))
                    return "ArXiv Researcher";
                if (domain.Contains("vnu") || domain.Contains("hust") || domain.Contains("fpt"))
                    return "Academic Researcher";
                if (domain.Contains("gov"))
                    return "Government Agency";
                
                return $"{domain.Replace("www.", "").Split('.')[0]} Author";
            }
            
            return "Unknown Author";
        }

        private static string GenerateSmartPublisher(string? sourceUrl)
        {
            if (!string.IsNullOrWhiteSpace(sourceUrl))
            {
                try
                {
                    var uri = new Uri(sourceUrl);
                    var domain = uri.Host.ToLower().Replace("www.", "");
                    
                    if (domain.Contains("wikipedia"))
                        return "Wikimedia Foundation";
                    if (domain.Contains("researchgate"))
                        return "ResearchGate";
                    if (domain.Contains("arxiv"))
                        return "arXiv.org";
                    if (domain.Contains("ieee"))
                        return "IEEE";
                    if (domain.Contains("acm"))
                        return "ACM";
                    if (domain.Contains("springer"))
                        return "Springer";
                    if (domain.Contains("elsevier"))
                        return "Elsevier";
                    if (domain.Contains("nature"))
                        return "Nature Publishing Group";
                    
                    // Capitalize first letter of domain
                    var parts = domain.Split('.');
                    return parts.Length > 0 ? 
                           char.ToUpper(parts[0][0]) + parts[0].Substring(1) : 
                           "Unknown Publisher";
                }
                catch
                {
                    return "Web Publisher";
                }
            }
            
            return "Unknown Publisher";
        }

        private static string GenerateSmartSource(string? sourceUrl)
        {
            if (!string.IsNullOrWhiteSpace(sourceUrl))
            {
                try
                {
                    var uri = new Uri(sourceUrl);
                    return uri.Host.Replace("www.", "");
                }
                catch
                {
                    return "Web Source";
                }
            }
            
            return "Unknown Source";
        }
    }
}