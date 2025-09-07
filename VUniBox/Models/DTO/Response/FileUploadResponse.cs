using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Response for file upload and classification step
    /// </summary>
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
}