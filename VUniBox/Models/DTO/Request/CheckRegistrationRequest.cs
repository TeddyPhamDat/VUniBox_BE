using System.ComponentModel.DataAnnotations;

namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Request for checking email and phone availability in registration
    /// </summary>
    public class CheckRegistrationRequest
    {
        /// <summary>
        /// Email address
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Phone number (can be in Vietnamese or international format)
        /// </summary>
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}