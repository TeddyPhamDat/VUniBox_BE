using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Response for URL processing and classification step
    /// </summary>
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
}