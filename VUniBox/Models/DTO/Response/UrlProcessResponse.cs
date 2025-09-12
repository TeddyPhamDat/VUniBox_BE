using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Response for URL processing and classification step
    /// </summary>
    public class UrlProcessResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the URL that was processed.
        /// </summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the detected document type.
        /// </summary>
        public DocumentType DetectedType { get; set; }
        /// <summary>
        /// Gets or sets the human-readable name of the detected document type.
        /// </summary>
        public string TypeName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets a confirmation message to be displayed to the user.
        /// </summary>
        public string ConfirmationMessage { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets a question to prompt the user for a decision (e.g., "Bạn có muốn lưu URL này không?").
        /// </summary>
        public string Question { get; set; } = string.Empty; // "B?n c mu?n l?u URL ny khng?"
        /// <summary>
        /// Gets or sets a subtitle providing additional context for the user's decision (e.g., "Bạn có thể lưu hoặc không lưu URL trong thao tác").
        /// </summary>
        public string Subtitle { get; set; } = string.Empty; // "B?n c th? l?u ho?c khng l?u URL trong thao tc"
        /// <summary>
        /// Gets or sets a temporary ID for tracking the session.
        /// </summary>
        public string TempId { get; set; } = string.Empty;
    }
}