using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Represents a request for document classification, specifying the input type (file or URL) and relevant identifier.
    /// </summary>
    public class ClassificationRequest
    {
        /// <summary>
        /// Gets or sets the type of input for classification (File or Url).
        /// </summary>
        public InputType InputType { get; set; }
        /// <summary>
        /// Gets or sets the file name for file uploads, if applicable.
        /// </summary>
        public string? FileName { get; set; } // For file upload
        /// <summary>
        /// Gets or sets the URL for URL input, if applicable.
        /// </summary>
        public string? Url { get; set; } // For URL input
    }
}

