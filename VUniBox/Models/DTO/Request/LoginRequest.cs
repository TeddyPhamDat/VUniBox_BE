namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Represents a request for user login.
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// Gets or sets the username for login.
        /// </summary>
        public string Username { get; set; }
        /// <summary>
        /// Gets or sets the password for login.
        /// </summary>
        public string Password { get; set; }
    }
}
