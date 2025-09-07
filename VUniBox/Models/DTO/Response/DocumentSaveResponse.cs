using VUniBox.Models;
using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Response for document save operations
    /// Used when saving documents to folder or moving to trash
    /// </summary>
    public class DocumentSaveResponse
    {
        public bool Success { get; set; }
        public DocumentDto? Document { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public bool IsInTrash { get; set; } = false;
        public DateTime? ExpiryDate { get; set; }
    }
}








