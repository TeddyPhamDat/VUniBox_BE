namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Response for OTP sending request
    /// </summary>
    public class SendOtpResponse
    {
        /// <summary>
        /// Session ID for OTP verification
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Masked phone number for display (e.g., +84*****4567)
        /// </summary>
        public string MaskedPhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// OTP expiration time in minutes
        /// </summary>
        public int ExpirationMinutes { get; set; } = 5;

        /// <summary>
        /// Message for user
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response for OTP verification
    /// </summary>
    public class VerifyOtpResponse
    {
        /// <summary>
        /// Whether verification was successful
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// Session ID (same as input if successful)
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Verified phone number (only if successful)
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Message for user
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response for phone-based login
    /// </summary>
    public class PhoneLoginResponse
    {
        /// <summary>
        /// User ID
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// JWT access token
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Token expiration time
        /// </summary>
        public DateTime TokenExpiresAt { get; set; }

        /// <summary>
        /// User's full name
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Verified phone number
        /// </summary>
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// User role
        /// </summary>
        public string Role { get; set; } = string.Empty;
    }
}