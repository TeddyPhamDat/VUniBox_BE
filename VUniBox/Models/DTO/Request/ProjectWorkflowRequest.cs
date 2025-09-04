using VUniBox.Models.DTO;
using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Request
{
    // Request for URL input
    public class UrlInputRequest
    {
        public string Url { get; set; } = string.Empty;
    }

    // Request for user confirmation after classification
    public class UserConfirmationRequest
    {
        public int UserId { get; set; }
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public string? Url { get; set; }
        public DocumentType DocumentType { get; set; }
        public bool SaveToFolder { get; set; } // true = save to folder, false = move to trash
        public string TempId { get; set; } = string.Empty; // For tracking the session
    }

    // Keep existing requests for backward compatibility
    public class UrlProcessRequest
    {
        public string Url { get; set; } = string.Empty;
    }

    public class UserDecisionRequest
    {
        public int UserId { get; set; }
        public DocumentMetadataDto Metadata { get; set; } = new();
        public DocumentType DocumentType { get; set; }
        public string? FilePath { get; set; }
        public bool SaveToFolder { get; set; } // true = save, false = trash
    }
}