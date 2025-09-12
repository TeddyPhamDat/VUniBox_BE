using VUniBox.Models.DTO.Request;

namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Request for URL input processing
    /// Used when user wants to process a URL for document classification
    /// </summary>
    public class UrlInputRequest
    {
        /// <summary>
        /// Gets or sets the URL to be processed.
        /// </summary>
        public string Url { get; set; } = string.Empty;
    }
}