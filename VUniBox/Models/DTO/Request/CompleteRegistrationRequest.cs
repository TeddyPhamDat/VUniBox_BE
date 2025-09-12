namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Represents a request to complete user registration after OTP verification.
    /// </summary>
    public class CompleteRegistrationRequest
    {
        /// <summary>
        /// Gets or sets the email address of the user.
        /// </summary>
        public string Email { get; set; }
        /// <summary>
        /// Gets or sets the One-Time Password (OTP) token received by the user.
        /// </summary>
        public string OtpToken { get; set; }
        /// <summary>
        /// Gets or sets the chosen password for the user account.
        /// </summary>
        public string Password { get; set; }
        /// <summary>
        /// Gets or sets the full name of the user.
        /// </summary>
        public string FullName { get; set; }
    }
}
