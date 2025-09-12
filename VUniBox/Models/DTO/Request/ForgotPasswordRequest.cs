namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Represents a request for a password reset. Typically contains the user's email.
    /// </summary>
    public class ForgotPasswordRequest
    {
        /// <summary>
        /// Gets or sets the email address associated with the account that needs a password reset.
        /// </summary>
        public string Email { get; set; }
    }
}
