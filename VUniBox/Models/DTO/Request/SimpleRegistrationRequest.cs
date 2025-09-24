using System.ComponentModel.DataAnnotations;

namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Represents a simple one-step registration request with all required information.
    /// </summary>
    public class SimpleRegistrationRequest
    {
        /// <summary>
        /// Gets or sets the email address for the user account.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full name of the user.
        /// </summary>
        [Required]
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the phone number of the user.
        /// </summary>
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the chosen password for the user account.
        /// </summary>
        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password confirmation.
        /// </summary>
        [Required]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}