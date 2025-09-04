using VUniBox.Models.DTO;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Response
{
    // Response for file upload step
    public class FileUploadResponse
    {
        public bool Success { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DocumentType DetectedType { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string ConfirmationMessage { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty; // "B?n có mu?n l?u file này không?"
        public string Subtitle { get; set; } = string.Empty; // "B?n có th? l?u ho?c không l?u file trong thao tác"
        public string TempId { get; set; } = string.Empty;
    }

    // Response for URL processing step
    public class UrlProcessResponse
    {
        public bool Success { get; set; }
        public string Url { get; set; } = string.Empty;
        public DocumentType DetectedType { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string ConfirmationMessage { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty; // "B?n có mu?n l?u URL này không?"
        public string Subtitle { get; set; } = string.Empty; // "B?n có th? l?u ho?c không l?u URL trong thao tác"
        public string TempId { get; set; } = string.Empty;
    }

    // Response for final action confirmation - Contains complete metadata for citation generation
    public class ActionConfirmationResponse
    {
        public bool Success { get; set; }
        public int DocumentId { get; set; }
        public string Action { get; set; } = string.Empty; // "saved" or "moved_to_trash"
        public string Message { get; set; } = string.Empty;
        public bool IsInTrash { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Location { get; set; } = string.Empty;
        
        // Complete metadata for citation generation
        public CitationMetadataDto Metadata { get; set; } = new();
    }

    // Enhanced metadata DTO specifically for citation generation
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
        
        public CitationMetadataDto(Documents document, DocumentMetadataDto extractedMetadata)
        {
            // Document info
            DocumentId = document.DocumentId;
            Type = document.Type ?? string.Empty;
            Status = document.Status ?? string.Empty;
            CreatedAt = document.CreatedAt;
            
            // Core citation fields - prefer extracted metadata, fallback to document
            Title = !string.IsNullOrEmpty(extractedMetadata.Title) ? extractedMetadata.Title : (document.Title ?? string.Empty);
            Author = !string.IsNullOrEmpty(extractedMetadata.Author) ? extractedMetadata.Author : (document.Author ?? string.Empty);
            Authors = !string.IsNullOrEmpty(extractedMetadata.Authors) ? extractedMetadata.Authors : (document.Authors ?? string.Empty);
            PublicationDate = extractedMetadata.PublicationDate ?? document.PublicationDate;
            
            // Publication details
            Publisher = !string.IsNullOrEmpty(extractedMetadata.Publisher) ? extractedMetadata.Publisher : (document.Publisher ?? string.Empty);
            Journal = !string.IsNullOrEmpty(extractedMetadata.Journal) ? extractedMetadata.Journal : (document.Journal ?? string.Empty);
            Volume = !string.IsNullOrEmpty(extractedMetadata.Volume) ? extractedMetadata.Volume : (document.Volume ?? string.Empty);
            Issue = !string.IsNullOrEmpty(extractedMetadata.Issue) ? extractedMetadata.Issue : (document.Issue ?? string.Empty);
            Pages = !string.IsNullOrEmpty(extractedMetadata.Pages) ? extractedMetadata.Pages : (document.Pages ?? string.Empty);
            
            // Identifiers
            Doi = !string.IsNullOrEmpty(extractedMetadata.DOI) ? extractedMetadata.DOI : (document.Doi ?? string.Empty);
            Isbn = !string.IsNullOrEmpty(extractedMetadata.ISBN) ? extractedMetadata.ISBN : (document.Isbn ?? string.Empty);
            URL = !string.IsNullOrEmpty(extractedMetadata.URL) ? extractedMetadata.URL : (document.SourceUrl ?? string.Empty);
            
            // Content
            Abstract = !string.IsNullOrEmpty(extractedMetadata.Abstract) ? extractedMetadata.Abstract : (document.Abstract ?? string.Empty);
            Keywords = !string.IsNullOrEmpty(extractedMetadata.Keywords) ? extractedMetadata.Keywords : (document.Keywords ?? string.Empty);
            Subject = !string.IsNullOrEmpty(extractedMetadata.Subject) ? extractedMetadata.Subject : (document.Subject ?? string.Empty);
            Language = !string.IsNullOrEmpty(extractedMetadata.Language) ? extractedMetadata.Language : (document.Language ?? string.Empty);
            
            // Source info
            Source = !string.IsNullOrEmpty(extractedMetadata.Source) ? extractedMetadata.Source : (document.Source ?? string.Empty);
            RetrievedDate = extractedMetadata.RetrievedDate ?? document.RetrievedDate;
            
            // File info
            FilePath = !string.IsNullOrEmpty(extractedMetadata.FilePath) ? extractedMetadata.FilePath : (document.FilePath ?? string.Empty);
            FileSize = extractedMetadata.FileSize;
            FileType = !string.IsNullOrEmpty(extractedMetadata.FileType) ? extractedMetadata.FileType : string.Empty;
        }
    }
}