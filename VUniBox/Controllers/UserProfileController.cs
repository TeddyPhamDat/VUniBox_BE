using Microsoft.AspNetCore.Mvc;
using VUniBox.Models.DTO.Response;
using VUniBox.Services.Usage;
using VUniBox.DBContext;
using Microsoft.EntityFrameworkCore;

namespace VUniBox.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    /// <summary>
    /// Controller for managing user profiles and retrieving usage statistics.
    /// </summary>
    public class UserProfileController : ControllerBase
    {
        private readonly VUniBoxContext _context;
        private readonly IUsageTrackingService _usageTrackingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserProfileController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="usageTrackingService">The usage tracking service.</param>
        public UserProfileController(VUniBoxContext context, IUsageTrackingService usageTrackingService)
        {
            _context = context;
            _usageTrackingService = usageTrackingService;
        }

        /// <summary>
        /// Retrieves the user profile along with their usage statistics.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>An <see cref="IActionResult"/> with the user profile and usage details.</returns>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserProfile(int userId)
        {
            try
            {
                // Get user information
                var user = await _context.Users
                    .Include(u => u.CurrentPlan)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    return NotFound(ApiResponse<UserProfileResponse>.Fail("Không tìm thấy người dùng", 404));
                }

                // Update storage usage first
                await _usageTrackingService.UpdateStorageUsageAsync(userId);

                // Get usage statistics
                var usage = await _usageTrackingService.GetUserUsageAsync(userId);
                var (usedMb, limitMb, totalFiles) = await _usageTrackingService.GetStorageInfoAsync(userId);

                var userProfile = new UserProfileDto
                {
                    UserId = user.UserId,
                    FullName = user.FullName ?? "Unknown",
                    Email = user.Email ?? "",
                    Role = ((Models.Enum.Role)user.Role).ToString(),
                    CreatedAt = user.CreatedAt,
                    PlanName = user.CurrentPlan?.PlanName ?? "FREE",
                    PlanExpiryDate = user.PlanExpiryDate.HasValue ? user.PlanExpiryDate.Value.ToDateTime(TimeOnly.MinValue) : null,
                    UsageStats = new UsageStatsDto
                    {
                        StorageUsedMb = usedMb,
                        StorageLimitMb = limitMb,
                        TotalFiles = totalFiles,
                        CitationUsed = usage.CitationUsed ?? 0,
                        ChatbotUsed = usage.ChatbotUsed ?? 0,
                        CitationLimit = user.CurrentPlan?.CitationLimit ?? 10, // Free plan default
                        ChatbotLimit = user.CurrentPlan?.ChatbotLimit ?? 50,   // Free plan default
                        LastUpdated = usage.LastUpdated
                    }
                };

                var response = new UserProfileResponse
                {
                    Success = true,
                    UserProfile = userProfile,
                    Message = "Thông tin người dùng được truy xuất thành công"
                };

                return Ok(ApiResponse<UserProfileResponse>.Success(response, "Thông tin người dùng được truy xuất"));
            }
            catch (Exception ex)
            {
                var errorResponse = new UserProfileResponse
                {
                    Success = false,
                    Message = "Không thể truy xuất thông tin người dùng",
                    Error = ex.Message
                };

                return StatusCode(500, ApiResponse<UserProfileResponse>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Retrieves storage usage details for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>An <see cref="IActionResult"/> with storage usage details.</returns>
        [HttpGet("{userId}/storage")]
        public async Task<IActionResult> GetStorageUsage(int userId)
        {
            try
            {
                // Update storage usage first
                await _usageTrackingService.UpdateStorageUsageAsync(userId);

                var (usedMb, limitMb, totalFiles) = await _usageTrackingService.GetStorageInfoAsync(userId);

                var storageInfo = new
                {
                    UsedMb = usedMb,
                    LimitMb = limitMb,
                    UsagePercentage = limitMb > 0 ? (double)usedMb / limitMb * 100 : 0,
                    TotalFiles = totalFiles,
                    RemainingMb = limitMb - usedMb,
                    IsNearLimit = limitMb > 0 && (double)usedMb / limitMb > 0.8, // 80% threshold
                    FormattedUsage = $"{usedMb}MB / {limitMb}MB"
                };

                return Ok(ApiResponse<object>.Success(storageInfo, "Thông tin dung lượng được truy xuất"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Refreshes and recalculates user usage statistics.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>An <see cref="IActionResult"/> with the updated usage statistics.</returns>
        [HttpPost("{userId}/refresh-usage")]
        public async Task<IActionResult> RefreshUsageStats(int userId)
        {
            try
            {
                // Recalculate storage usage from actual files
                await _usageTrackingService.UpdateStorageUsageAsync(userId);

                var usage = await _usageTrackingService.GetUserUsageAsync(userId);
                var (usedMb, limitMb, totalFiles) = await _usageTrackingService.GetStorageInfoAsync(userId);

                var refreshedStats = new
                {
                    StorageUsedMb = usedMb,
                    StorageLimitMb = limitMb,
                    TotalFiles = totalFiles,
                    CitationUsed = usage.CitationUsed ?? 0,
                    ChatbotUsed = usage.ChatbotUsed ?? 0,
                    LastUpdated = usage.LastUpdated,
                    Message = "Thống kê đã được cập nhật thành công"
                };

                return Ok(ApiResponse<object>.Success(refreshedStats, "Thống kê đã được làm mới"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }
    }
}
