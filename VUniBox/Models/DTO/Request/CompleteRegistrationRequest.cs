using System.ComponentModel.DataAnnotations;

namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Represents a request to complete user registration with password confirmation.
    /// Email is already stored in session from step 1.
    /// </summary>
    public class CompleteRegistrationRequest
    {
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

        /// <summary>
        /// Gets or sets the full name of the user.
        /// </summary>
        [Required]
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the registration token from the check step.
        /// </summary>
        [Required]
        public string RegistrationToken { get; set; } = string.Empty;
    }
}
