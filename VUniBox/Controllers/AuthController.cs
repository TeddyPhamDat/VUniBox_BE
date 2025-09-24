using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.SqlServer.Server;
using System.Security.Claims;
using System.Text.Json;
using System.Linq;
using VUniBox.DBContext;
using VUniBox.Models;
using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.Enum;
using VUniBox.Services.Authentication;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Google.Apis.Auth;

namespace VUniBox.Controllers
{
    /// <summary>
    /// Controller for authentication and registration flows.
    /// Simple authentication without OTP/SMS verification.
    /// </summary>
    [ApiController]
    [Route("api/authentication")]
    public class AuthController : ControllerBase
    {
        private readonly IJwtService _jwtService;
        private readonly VUniBoxContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="jwtService">The JWT service for token generation.</param>
        /// <param name="context">The database context.</param>
        public AuthController(IJwtService jwtService, VUniBoxContext context)
        {
            _jwtService = jwtService;
            _context = context;
        }

        /// <summary>
        /// Login with email and password
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(ApiResponse<LoginResponse>.Fail("Email và mật khẩu không được để trống", 400));
                }

                // Find user by email
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (user == null)
                {
                    return BadRequest(ApiResponse<LoginResponse>.Fail("Email hoặc mật khẩu không đúng", 400));
                }

                // Verify password
                if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    return BadRequest(ApiResponse<LoginResponse>.Fail("Email hoặc mật khẩu không đúng", 400));
                }

                // Check if user is active
                if (user.IsActive != true)
                {
                    return BadRequest(ApiResponse<LoginResponse>.Fail("Tài khoản đã bị khóa", 400));
                }

                // Generate JWT token
                var tokens = _jwtService.GenerateTokens(user);

                var response = new LoginResponse
                {
                    UserId = user.UserId,
                    Token = tokens.AccessToken,
                    TokenExpiresAt = tokens.AccessTokenExpireAt,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = user.Role.ToString()
                };

                return Ok(ApiResponse<LoginResponse>.Success(response, "Đăng nhập thành công"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<LoginResponse>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Simple one-step registration with all required information
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] SimpleRegistrationRequest request)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(request.Email) || 
                    string.IsNullOrWhiteSpace(request.PhoneNumber) ||
                    string.IsNullOrWhiteSpace(request.FullName) ||
                    string.IsNullOrWhiteSpace(request.Password) ||
                    string.IsNullOrWhiteSpace(request.ConfirmPassword))
                {
                    return BadRequest(ApiResponse<LoginResponse>.Fail("Tất cả các trường không được để trống", 400));
                }

                // Validate email format
                if (!IsValidEmail(request.Email))
                {
                    return BadRequest(ApiResponse<LoginResponse>.Fail("Định dạng email không hợp lệ", 400));
                }

                // Validate Vietnamese phone number format
                if (!IsValidVietnamesePhoneNumber(request.PhoneNumber))
                {
                    return BadRequest(ApiResponse<LoginResponse>.Fail("Số điện thoại phải có 10 chữ số và bắt đầu bằng số 0", 400));
                }

                // Check password confirmation
                if (request.Password != request.ConfirmPassword)
                {
                    return BadRequest(ApiResponse<LoginResponse>.Fail("Mật khẩu xác nhận không khớp", 400));
                }

                // Password strength validation
                if (request.Password.Length < 8)
                {
                    return BadRequest(ApiResponse<LoginResponse>.Fail("Mật khẩu phải có ít nhất 8 ký tự", 400));
                }

                // Convert Vietnamese phone number to international format
                var convertedPhone = request.PhoneNumber;
                if (request.PhoneNumber.StartsWith("0") && request.PhoneNumber.Length >= 10)
                {
                    convertedPhone = "+84" + request.PhoneNumber.Substring(1);
                }

                // Check if email already exists
                var existingUserByEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (existingUserByEmail != null)
                {
                    return BadRequest(ApiResponse<LoginResponse>.Fail("Email đã được đăng ký", 400));
                }

                // Check if phone number already exists
                var existingUserByPhone = await _context.Users.FirstOrDefaultAsync(u => 
                    u.PhoneNumber == request.PhoneNumber || u.PhoneNumber == convertedPhone);
                if (existingUserByPhone != null)
                {
                    return BadRequest(ApiResponse<LoginResponse>.Fail("Số điện thoại đã được đăng ký", 400));
                }

                // Hash password
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

                // Create new user
                var newUser = new Users
                {
                    Email = request.Email,
                    PhoneNumber = convertedPhone,
                    FullName = request.FullName,
                    PasswordHash = passwordHash,
                    Role = (int)Role.USER,
                    IsVerified = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                // Assign FREE plan
                var freePlan = await _context.Plans.FirstOrDefaultAsync(p => p.PlanName == "FREE" && p.Price == 0);
                if (freePlan != null)
                {
                    newUser.CurrentPlanId = freePlan.PlanId;
                    newUser.PlanExpiryDate = null;
                }

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                // Create subscription and usage stats
                if (freePlan != null)
                {
                    var subscription = new Subscriptions
                    {
                        UserId = newUser.UserId,
                        PlanId = freePlan.PlanId,
                        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(100)),
                        Status = "ACTIVE",
                        PaymentId = null
                    };
                    _context.Subscriptions.Add(subscription);

                    var usageStats = new UsageStats
                    {
                        UserId = newUser.UserId,
                        StorageUsedMb = 0,
                        CitationUsed = 0,
                        ChatbotUsed = 0,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.UsageStats.Add(usageStats);
                    await _context.SaveChangesAsync();
                }

                // Generate JWT token
                var tokens = _jwtService.GenerateTokens(newUser);

                var response = new LoginResponse
                {
                    UserId = newUser.UserId,
                    Token = tokens.AccessToken,
                    TokenExpiresAt = tokens.AccessTokenExpireAt,
                    FullName = newUser.FullName,
                    Email = newUser.Email,
                    Role = newUser.Role.ToString()
                };

                return Ok(ApiResponse<LoginResponse>.Success(response, "Đăng ký thành công"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<LoginResponse>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Authenticates a user using Google OAuth and creates/updates a user session.
        /// </summary>
        /// <param name="request">The Google login request containing the ID token.</param>
        /// <returns>An <see cref="IActionResult"/> with the login response including tokens.</returns>
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                GoogleJsonWebSignature.Payload payload;
                try
                {
                    payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);
                }
                catch (InvalidJwtException)
                {
                    return BadRequest(ApiResponse<string>.Fail("Mã thông báo Google không hợp lệ", 401));
                }

                var email = payload.Email;
                if (string.IsNullOrEmpty(email))
                    return BadRequest(ApiResponse<string>.Fail("Không thể lấy email từ Google", 400));

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                {
                    // Register new user from Google account
                    user = new Users
                    {
                        Email = email,
                        FullName = payload.Name,
                        Role = (int)Role.USER,
                        IsVerified = true,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    // Assign FREE plan
                    var freePlan = await _context.Plans.FirstOrDefaultAsync(p => p.PlanName == "FREE" && p.Price == 0);
                    if (freePlan != null)
                    {
                        user.CurrentPlanId = freePlan.PlanId;
                        user.PlanExpiryDate = null;
                    }

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    // Create subscription and usage stats
                    if (freePlan != null)
                    {
                        var subscription = new Subscriptions
                        {
                            UserId = user.UserId,
                            PlanId = freePlan.PlanId,
                            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(100)),
                            Status = "ACTIVE",
                            PaymentId = null
                        };
                        _context.Subscriptions.Add(subscription);

                        var usageStats = new UsageStats
                        {
                            UserId = user.UserId,
                            StorageUsedMb = 0,
                            CitationUsed = 0,
                            ChatbotUsed = 0,
                            LastUpdated = DateTime.UtcNow
                        };
                        _context.UsageStats.Add(usageStats);
                        await _context.SaveChangesAsync();
                    }
                }

                // Check if user is active
                if (user.IsActive != true)
                {
                    return BadRequest(ApiResponse<string>.Fail("Tài khoản đã bị khóa", 400));
                }

                var tokens = _jwtService.GenerateTokens(user);

                var response = new LoginResponse
                {
                    UserId = user.UserId,
                    Token = tokens.AccessToken,
                    TokenExpiresAt = tokens.AccessTokenExpireAt,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = ((Role)user.Role).ToString()
                };

                return Ok(ApiResponse<LoginResponse>.Success(response, "Đăng nhập thành công với Google"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Step 1: Verify email and phone for forgot password
        /// </summary>
        [HttpPost("forgot-password/verify")]
        public async Task<IActionResult> VerifyForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    return BadRequest(ApiResponse<string>.Fail("Email và số điện thoại không được để trống", 400));
                }

                // Validate email format
                if (!IsValidEmail(request.Email))
                {
                    return BadRequest(ApiResponse<string>.Fail("Định dạng email không hợp lệ", 400));
                }

                // Validate Vietnamese phone number format (10 digits starting with 0)
                if (!IsValidVietnamesePhoneNumber(request.PhoneNumber))
                {
                    return BadRequest(ApiResponse<string>.Fail("Số điện thoại phải có 10 chữ số và bắt đầu bằng số 0", 400));
                }

                // Convert phone number if needed
                var convertedPhone = request.PhoneNumber;
                if (request.PhoneNumber.StartsWith("0") && request.PhoneNumber.Length >= 10)
                {
                    convertedPhone = "+84" + request.PhoneNumber.Substring(1);
                }

                // Check if user exists with both email and phone
                var user = await _context.Users.FirstOrDefaultAsync(u => 
                    u.Email == request.Email && 
                    (u.PhoneNumber == request.PhoneNumber || u.PhoneNumber == convertedPhone));

                if (user == null)
                {
                    return BadRequest(ApiResponse<string>.Fail("Không tìm thấy tài khoản với email và số điện thoại này", 400));
                }

                if (user.IsActive != true)
                {
                    return BadRequest(ApiResponse<string>.Fail("Tài khoản đã bị khóa", 400));
                }

                // Store verified user info in session for password reset
                HttpContext.Session.SetString("ForgotPassword_Email", request.Email);
                HttpContext.Session.SetString("ForgotPassword_Phone", convertedPhone);
                HttpContext.Session.SetString("ForgotPassword_Expiry", DateTime.UtcNow.AddMinutes(15).ToString());

                return Ok(ApiResponse<string>.Success("", "Xác thực thành công. Bạn có thể đặt lại mật khẩu mới."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Step 2: Reset password using session data from verification step
        /// </summary>
        [HttpPost("forgot-password/reset")]
        public async Task<IActionResult> ResetForgotPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.NewPassword) || 
                    string.IsNullOrWhiteSpace(request.ConfirmPassword))
                {
                    return BadRequest(ApiResponse<string>.Fail("Mật khẩu mới và xác nhận mật khẩu không được để trống", 400));
                }

                if (request.NewPassword != request.ConfirmPassword)
                {
                    return BadRequest(ApiResponse<string>.Fail("Mật khẩu xác nhận không khớp", 400));
                }

                if (request.NewPassword.Length < 6)
                {
                    return BadRequest(ApiResponse<string>.Fail("Mật khẩu phải có ít nhất 6 ký tự", 400));
                }

                // Get verified user info from session
                var sessionEmail = HttpContext.Session.GetString("ForgotPassword_Email");
                var sessionPhone = HttpContext.Session.GetString("ForgotPassword_Phone");
                var sessionExpiry = HttpContext.Session.GetString("ForgotPassword_Expiry");

                if (string.IsNullOrEmpty(sessionEmail) || string.IsNullOrEmpty(sessionPhone) || string.IsNullOrEmpty(sessionExpiry))
                {
                    return BadRequest(ApiResponse<string>.Fail("Vui lòng thực hiện xác thực email và số điện thoại trước", 400));
                }

                // Check if session has expired
                if (DateTime.TryParse(sessionExpiry, out var expiryTime) && DateTime.UtcNow > expiryTime)
                {
                    // Clear expired session
                    HttpContext.Session.Remove("ForgotPassword_Email");
                    HttpContext.Session.Remove("ForgotPassword_Phone");
                    HttpContext.Session.Remove("ForgotPassword_Expiry");
                    return BadRequest(ApiResponse<string>.Fail("Phiên xác thực đã hết hạn. Vui lòng thực hiện lại việc xác thực", 400));
                }

                // Find user by email and phone from session
                var user = await _context.Users.FirstOrDefaultAsync(u => 
                    u.Email == sessionEmail && u.PhoneNumber == sessionPhone);

                if (user == null)
                {
                    return BadRequest(ApiResponse<string>.Fail("Không tìm thấy tài khoản", 400));
                }

                if (user.IsActive != true)
                {
                    return BadRequest(ApiResponse<string>.Fail("Tài khoản đã bị khóa", 400));
                }

                // Hash new password
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                user.PasswordHash = hashedPassword;

                await _context.SaveChangesAsync();

                // Clear session after successful password reset
                HttpContext.Session.Remove("ForgotPassword_Email");
                HttpContext.Session.Remove("ForgotPassword_Phone");
                HttpContext.Session.Remove("ForgotPassword_Expiry");

                return Ok(ApiResponse<string>.Success("", "Đặt lại mật khẩu thành công"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Validates email format using regular expression
        /// </summary>
        /// <param name="email">The email to validate</param>
        /// <returns>True if email format is valid, false otherwise</returns>
        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Use System.ComponentModel.DataAnnotations.EmailAddressAttribute for validation
                var emailAttribute = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
                return emailAttribute.IsValid(email);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates Vietnamese phone number format (10 digits starting with 0)
        /// </summary>
        /// <param name="phoneNumber">The phone number to validate</param>
        /// <returns>True if phone number format is valid, false otherwise</returns>
        private bool IsValidVietnamesePhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            // Remove any spaces or special characters
            var cleanedPhone = phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

            // Check if it's exactly 10 digits and starts with 0
            if (cleanedPhone.Length != 10)
                return false;

            if (!cleanedPhone.StartsWith("0"))
                return false;

            // Check if all characters are digits
            return cleanedPhone.All(char.IsDigit);
        }
    }
}
