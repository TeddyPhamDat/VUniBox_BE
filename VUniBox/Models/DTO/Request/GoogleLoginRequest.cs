namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Represents a request for Google login, containing the ID token received from Google.
    /// </summary>
    public class GoogleLoginRequest
    {
        /// <summary>
        /// Gets or sets the Google ID token.
        /// </summary>
        public string IdToken { get; set; } = string.Empty;
    }
}
