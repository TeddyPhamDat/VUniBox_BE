using VUniBox.Models;
using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Response
{
    public class DocumentSaveResponse
    {
        public bool Success { get; set; }
        public DocumentDto? Document { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public bool IsInTrash { get; set; } = false;
        public DateTime? ExpiryDate { get; set; }
    }

    // DTO to prevent circular reference issues
    public class DocumentDto
    {
        public int DocumentId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Author { get; set; } = string.Empty;
        public string Authors { get; set; } = string.Empty;
        public DateOnly? PublicationDate { get; set; }
        public string Publisher { get; set; } = string.Empty;
        public string Journal { get; set; } = string.Empty;
        public string Volume { get; set; } = string.Empty;
        public string Issue { get; set; } = string.Empty;
        public string Pages { get; set; } = string.Empty;
        public string Doi { get; set; } = string.Empty;
        public string Isbn { get; set; } = string.Empty;
        public string Abstract { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public DateTime? RetrievedDate { get; set; }

        // Constructor to convert from Documents entity
        public DocumentDto() { }

        public DocumentDto(Documents document)
        {
            DocumentId = document.DocumentId;
            UserId = document.UserId;
            Title = document.Title ?? string.Empty;
            FilePath = document.FilePath ?? string.Empty;
            SourceUrl = document.SourceUrl ?? string.Empty;
            Type = document.Type ?? string.Empty;
            Status = document.Status ?? string.Empty;
            CreatedAt = document.CreatedAt;
            ExpiryDate = document.ExpiryDate;
            Author = document.Author ?? string.Empty;
            Authors = document.Authors ?? string.Empty;
            PublicationDate = document.PublicationDate;
            Publisher = document.Publisher ?? string.Empty;
            Journal = document.Journal ?? string.Empty;
            Volume = document.Volume ?? string.Empty;
            Issue = document.Issue ?? string.Empty;
            Pages = document.Pages ?? string.Empty;
            Doi = document.Doi ?? string.Empty;
            Isbn = document.Isbn ?? string.Empty;
            Abstract = document.Abstract ?? string.Empty;
            Description = document.Description ?? string.Empty;
            Keywords = document.Keywords ?? string.Empty;
            Subject = document.Subject ?? string.Empty;
            Source = document.Source ?? string.Empty;
            Language = document.Language ?? string.Empty;
            RetrievedDate = document.RetrievedDate;
        }
    }
}

