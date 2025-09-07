using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Response
{
    public class ClassificationResponse
    {
        public bool Success { get; set; }
        public DocumentType DocumentType { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Confidence { get; set; } // For future ML enhancement
    }
}


