namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Request for URL processing - kept for backward compatibility
    /// Consider using UrlInputRequest for new implementations
    /// </summary>
    [Obsolete("Consider using UrlInputRequest for new implementations")]
    public class UrlProcessRequest
    {
        public string Url { get; set; } = string.Empty;
    }
}