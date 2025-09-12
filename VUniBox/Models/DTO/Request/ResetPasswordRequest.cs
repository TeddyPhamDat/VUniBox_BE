namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Represents a request to reset a user's password using an OTP token.
    /// </summary>
    public class ResetPasswordRequest
    {
        /// <summary>
        /// Gets or sets the email address of the user.
        /// </summary>
        public string Email { get; set; }
        /// <summary>
        /// Gets or sets the One-Time Password (OTP) token provided for password reset.
        /// </summary>
        public string OtpToken { get; set; }
        /// <summary>
        /// Gets or sets the new password for the user account.
        /// </summary>
        public string NewPassword { get; set; }
    }
}
