using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SqlServer.Server;
using System.Security.Claims;
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
    /// Follows RESTful conventions: pluralized, hyphenated nouns for resource URIs.
    /// </summary>
    [ApiController]
    [Route("api/authentication")]
    public class AuthController : ControllerBase
    {
        private readonly IJwtService _jwtService;
        private readonly VUniBoxContext _context;
        private readonly IEmailSender _emailSender;

        public AuthController(JwtService jwtService, VUniBoxContext context, IEmailSender emailSender)
        {
            _jwtService = jwtService;
            _context = context;
            _emailSender = emailSender;
        }


        /// <summary>
        /// Authenticate user and return JWT access + refresh token.
        /// </summary>
        [HttpPost("login")]
        public IActionResult CreateSession([FromBody] Models.DTO.Request.LoginRequest request)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == request.Username);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Unauthorized(ApiResponse<object>.Fail("Invalid username or password", 401));

            // Tạo access token 
            var tokens = _jwtService.GenerateTokens(user);

            _context.SaveChanges();

            // Trả về response
            var response = new LoginResponse
            {
                UserId = user.UserId,
                Token = tokens.AccessToken,
                TokenExpiresAt = tokens.AccessTokenExpireAt,
                FullName = user.FullName,
                Email = user.Email,
                Role = ((Role)user.Role).ToString()
            };

            return Ok(ApiResponse<LoginResponse>.Success(response));
        }


        /// <summary>
        /// Initiate user registration and send an OTP (One-Time Password) to the provided email for verification.
        /// This step creates a new user as unverified if they don't already exist or handles resending OTP.
        /// </summary>
        [HttpPost("registrations")]
        public async Task<IActionResult> CreateRegistration([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
                return BadRequest(ApiResponse<string>.Fail("Email cannot be blank", 400));

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (existingUser != null)
            {
                if ((bool)existingUser.IsVerified)
                    return BadRequest(ApiResponse<string>.Fail("Email has been registered", 400));

                var oldOtp = await _context.OtpToken
                    .Where(o => o.Email == request.Email && o.Used == false)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefaultAsync();
                var otpCreatedAtUtc = DateTime.SpecifyKind((DateTime)oldOtp.CreatedAt, DateTimeKind.Utc);
                if (oldOtp != null && (DateTime.UtcNow - otpCreatedAtUtc).TotalMinutes < 1)
                {
                    return Ok(ApiResponse<string>.Success("", "OTP has been sent. Please check your email."));
                }

                // Send a new OTP if the previous one is expired or used.
                var newOtp = new OtpToken
                {
                    Email = request.Email,
                    Token = GenerateOtpToken(),
                    CreatedAt = DateTime.UtcNow,
                    Used = false
                };
                _context.OtpToken.Add(newOtp);
                await _context.SaveChangesAsync();

                await _emailSender.SendEmailAsync(request.Email, "VUniBox registration OTP", $"Your OTP code is: {newOtp.Token}");

                return Ok(ApiResponse<string>.Success("", "New OTP has been sent again"));
            }

            // Create a new user and send OTP for verification.
            var newUser = new VUniBox.Models.Users
            {
                Email = request.Email,
                Role = (int)Role.USER,
                IsVerified = false
            };
            _context.Users.Add(newUser);

            var otp = new OtpToken
            {
                Email = request.Email,
                Token = GenerateOtpToken(),
                CreatedAt = DateTime.UtcNow,
                Used = false
            };
            _context.OtpToken.Add(otp);

            await _context.SaveChangesAsync();
            await _emailSender.SendEmailAsync(request.Email, "VUniBox registration OTP", $"Your OTP code is: {otp.Token}");

            return Ok(ApiResponse<string>.Success("", "OTP sent to email"));
        }

        /// <summary>
        /// Completes the user registration process by verifying the OTP, setting the username, full name, and password.
        /// </summary>
        [HttpPost("registrations/complete")]
        public async Task<IActionResult> CompleteRegistration([FromBody] CompleteRegistrationRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.OtpToken)
                || string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.FullName))
            {
                return BadRequest(ApiResponse<string>.Fail("Email, OTP, Full name, and Password must not be empty", 400));
            }

            var otp = _context.OtpToken
                .Where(o => o.Email == request.Email && o.Token == request.OtpToken && o.Used == false)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault();

            if (otp == null)
                return BadRequest(ApiResponse<string>.Fail("Invalid or already used OTP", 400));

            var otpCreatedAtUtc = DateTime.SpecifyKind((DateTime)otp.CreatedAt, DateTimeKind.Utc);

            if ((DateTime.UtcNow - otpCreatedAtUtc).TotalMinutes > 1)
                return BadRequest(ApiResponse<string>.Fail("OTP has expired", 400));

            var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);

            if (user == null)
                return BadRequest(ApiResponse<string>.Fail("User not found", 400));

            if (user.IsVerified == true)
                return BadRequest(ApiResponse<string>.Fail("Account already verified", 400));

            otp.Used = true;
            user.IsVerified = true;
            user.FullName = request.FullName;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            user.Email = user.Email;
            user.CreatedAt = DateTime.UtcNow;
            user.IsActive = true;

            await _context.SaveChangesAsync();

            //    // Assign default quotas for new users.
            //    var periodStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            //    var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            //    var quotas = new List<UserQuotum>
            //{
            //    new UserQuotum { UserId = user.UserId, QuotaType = "video", QuotaLimit = 1, QuotaUsed = 0, PeriodStart = periodStart, PeriodEnd = periodEnd, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            //    new UserQuotum { UserId = user.UserId, QuotaType = "slides", QuotaLimit = 5, QuotaUsed = 0, PeriodStart = periodStart, PeriodEnd = periodEnd, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            //};

            //    _context.UserQuota.AddRange(quotas);
            //    await _context.SaveChangesAsync();

            var response = new LoginResponse
            {
                Token = "",
                TokenExpiresAt = null,
                FullName = user.FullName,
                Email = user.Email,
                Role = ((Role)user.Role).ToString()
            };

            return Ok(ApiResponse<LoginResponse>.Success(response, "Registration completed successfully"));
        }

        /// <summary>
        /// Authenticates a user using Google OAuth and creates/updates a user session.
        /// </summary>
        [HttpPost("google-sessions")]
        public async Task<IActionResult> CreateGoogleSession([FromBody] GoogleLoginRequest request)
        {
            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);
            }
            catch (InvalidJwtException)
            {
                return BadRequest(ApiResponse<string>.Fail("Invalid Token ID", 401));
            }

            var email = payload.Email;
            if (string.IsNullOrEmpty(email))
                return BadRequest(ApiResponse<string>.Fail("Can't get email from Google", 400));

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user != null)
            {
                if (user.IsVerified == false)
                {
                    return BadRequest(ApiResponse<string>.Fail("The account has not been authenticated via OTP", 400));
                }
            }
            else
            {
                // Register new user from Google account.
                user = new VUniBox.Models.Users
                {
                    Email = email,
                    FullName = payload.Name,
                    Role = (int)Role.USER,
                    IsVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            var tokens = _jwtService.GenerateTokens(user);

            var response = new LoginResponse
            {
                UserId = user.UserId,
                Token = tokens.AccessToken,
                TokenExpiresAt = null,
                FullName = user.FullName,
                Email = user.Email,
                Role = ((Role)user.Role).ToString()
            };

            return Ok(ApiResponse<LoginResponse>.Success(response, "Sign in successfully with Google"));
        }



        /// <summary>
        /// Initiates the forgot password process by sending a password reset OTP to the user's email.
        /// </summary>
        [HttpPost("forgot-password")]

        public async Task<IActionResult> ForgotPassword([FromBody] VUniBox.Models.DTO.Request.ForgotPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
                return BadRequest(ApiResponse<string>.Fail("Email is required", 400));

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || user.IsVerified != true)
                return BadRequest(ApiResponse<string>.Fail("Email is not registered or verified", 400));

            var otp = new OtpToken
            {
                Email = user.Email,
                Token = GenerateOtpToken(), // Random 6-digit code
                CreatedAt = DateTime.UtcNow,
                Used = false
            };

            _context.OtpToken.Add(otp);
            await _context.SaveChangesAsync();

            // Gửi email chứa mã OTP ở đây (tùy tích hợp)
            await _emailSender.SendEmailAsync(user.Email, "VUniBox Password Reset OTP", $"Your OTP code is: {otp.Token}");

            return Ok(ApiResponse<string>.Success("", "OTP has been sent to your email"));
        }

        /// <summary>
        /// Resets the user's password using a valid OTP and the new password.
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] VUniBox.Models.DTO.Request.ResetPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.OtpToken) || string.IsNullOrEmpty(request.NewPassword))
                return BadRequest(ApiResponse<string>.Fail("Email, OTP, and New Password are required", 400));

            var otp = await _context.OtpToken
                .Where(o => o.Email == request.Email && o.Token == request.OtpToken && o.Used == false)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null || (DateTime.UtcNow - otp.CreatedAt.Value).TotalMinutes > 5)
                return BadRequest(ApiResponse<string>.Fail("Invalid or expired OTP", 400));

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
                return BadRequest(ApiResponse<string>.Fail("User not found", 400));

            // Cập nhật mật khẩu
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            otp.Used = true;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Password has been reset successfully"));
        }




        /// <summary>
        /// Generates a 6-digit OTP code for email verification.
        /// </summary>
        private string GenerateOtpToken()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }
}
