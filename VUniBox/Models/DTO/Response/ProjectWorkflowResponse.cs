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

    // Response for final action confirmation
    public class ActionConfirmationResponse
    {
        public bool Success { get; set; }
        public DocumentDto Document { get; set; } = new();
        public string Action { get; set; } = string.Empty; // "saved" or "moved_to_trash"
        public string Message { get; set; } = string.Empty;
        public bool IsInTrash { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Location { get; set; } = string.Empty;
        public DocumentMetadataDto Metadata { get; set; } = new();
    }
}