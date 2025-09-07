using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Request for user confirmation after classification
    /// Used when user confirms their decision to save or trash a document
    /// after auto-classification step
    /// </summary>
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
}