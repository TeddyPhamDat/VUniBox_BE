using System.ComponentModel.DataAnnotations;

namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Request for sending OTP to phone number
    /// </summary>
    public class SendOtpRequest
    {
        /// <summary>
        /// Phone number in international format
        /// </summary>
        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request for verifying OTP code
    /// </summary>
    public class VerifyOtpRequest
    {
        /// <summary>
        /// Session ID from successful OTP sending
        /// </summary>
        [Required]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 6-digit OTP code received via SMS
        /// </summary>
        [Required]
        public string OtpCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Registration request - username, password, phone number
    /// </summary>
    public class RegisterRequest
    {
        /// <summary>
        /// Email address for login
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        /// <summary>
        /// Password (min 6 characters)
        /// </summary>
        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
        
        /// <summary>
        /// Phone number in international format
        /// </summary>
        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    /// <summary>
    /// Complete registration request - verify OTP and create account
    /// </summary>
    public class CompleteRegisterRequest
    {
        /// <summary>
        /// Session ID from successful OTP sending
        /// </summary>
        [Required]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 6-digit OTP code received via SMS
        /// </summary>
        [Required]
        public string Otp { get; set; } = string.Empty;

        /// <summary>
        /// User's full name
        /// </summary>
        [Required]
        public string FullName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Login request using email and password
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// Email address
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        /// <summary>
        /// Password
        /// </summary>
        [Required]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Forgot password request using email and phone number
    /// </summary>
    public class ForgotPasswordRequest
    {
        /// <summary>
        /// Email address
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Phone number for verification
        /// </summary>
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    /// <summary>
    /// Reset password request - includes email and phone for verification
    /// </summary>
    public class ResetPasswordRequest
    {
        /// <summary>
        /// Email address for verification
        /// </summary>
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Phone number for verification
        /// </summary>
        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// New password (min 6 characters)
        /// </summary>
        [Required(ErrorMessage = "Mật khẩu mới không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>
        /// Confirm new password
        /// </summary>
        [Required(ErrorMessage = "Xác nhận mật khẩu không được để trống")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}