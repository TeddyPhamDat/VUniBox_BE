using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Request
{
    public class ClassificationRequest
    {
        public InputType InputType { get; set; }
        public string? FileName { get; set; } // For file upload
        public string? Url { get; set; } // For URL input
    }
}

