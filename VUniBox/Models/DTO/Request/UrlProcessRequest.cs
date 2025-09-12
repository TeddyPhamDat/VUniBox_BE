namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Request for URL processing - kept for backward compatibility
    /// Consider using UrlInputRequest for new implementations
    /// </summary>
    [Obsolete("Consider using UrlInputRequest for new implementations")]
    public class UrlProcessRequest
    {
        /// <summary>
        /// Gets or sets the URL to be processed.
        /// </summary>
        public string Url { get; set; } = string.Empty;
    }
}