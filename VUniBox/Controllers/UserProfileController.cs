using Microsoft.AspNetCore.Mvc;
using VUniBox.Models.DTO.Response;
using VUniBox.Services.Usage;
using VUniBox.DBContext;
using Microsoft.EntityFrameworkCore;

namespace VUniBox.Controllers
{
    [ApiController]
    [Route("api/userProfile")]
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

                // Get document statistics
                var totalDocuments = await _context.Documents.CountAsync(d => d.UserId == userId);
                var savedDocuments = await _context.Documents.CountAsync(d => d.UserId == userId && d.Status == "Saved");
                var trashDocuments = await _context.Documents.CountAsync(d => d.UserId == userId && d.Status == "Trash");
                var totalCitations = await _context.Citations.CountAsync(c => c.UserId == userId);

                // Get activity statistics
                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;
                var documentsThisMonth = await _context.Documents
                    .CountAsync(d => d.UserId == userId && 
                                d.CreatedAt.HasValue && 
                                d.CreatedAt.Value.Month == currentMonth && 
                                d.CreatedAt.Value.Year == currentYear);
                
                var citationsThisMonth = await _context.Citations
                    .CountAsync(c => c.UserId == userId && 
                                c.CreatedAt.HasValue &&
                                c.CreatedAt.Value.Month == currentMonth && 
                                c.CreatedAt.Value.Year == currentYear);

                var lastDocumentUpload = await _context.Documents
                    .Where(d => d.UserId == userId)
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => d.CreatedAt)
                    .FirstOrDefaultAsync();

                var lastCitationGenerated = await _context.Citations
                    .Where(c => c.UserId == userId && c.CreatedAt.HasValue)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                // Get favorite document type
                var favoriteDocumentType = await _context.Documents
                    .Where(d => d.UserId == userId)
                    .GroupBy(d => d.DocumentType)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key.ToString())
                    .FirstOrDefaultAsync();

                var userProfile = new UserProfileDto
                {
                    UserId = user.UserId,
                    FullName = user.FullName ?? "Unknown",
                    Email = user.Email ?? "",
                    PhoneNumber = user.PhoneNumber,
                    AvatarUrl = user.AvatarUrl,
                    Role = ((Models.Enum.Role)user.Role).ToString(),
                    CreatedAt = user.CreatedAt,
                    IsVerified = user.IsVerified ?? false,
                    IsActive = user.IsActive ?? true,
                    CurrentPlanId = user.CurrentPlanId,
                    PlanName = user.CurrentPlan?.PlanName ?? "FREE",
                    PlanExpiryDate = user.PlanExpiryDate.HasValue ? user.PlanExpiryDate.Value.ToDateTime(TimeOnly.MinValue) : null,
                    TotalDocuments = totalDocuments,
                    SavedDocuments = savedDocuments,
                    TrashDocuments = trashDocuments,
                    TotalCitations = totalCitations,
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
                    },
                    ActivitySummary = new ActivitySummaryDto
                    {
                        DocumentsThisMonth = documentsThisMonth,
                        CitationsThisMonth = citationsThisMonth,
                        ChatbotThisMonth = usage.ChatbotUsed ?? 0, // Assuming chatbot usage is cumulative
                        LastDocumentUpload = lastDocumentUpload,
                        LastCitationGenerated = lastCitationGenerated,
                        FavoriteDocumentType = favoriteDocumentType ?? "Chưa có",
                        TotalActiveDays = await GetActiveDaysCount(userId)
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
        /// Updates user profile information.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="request">The profile update request.</param>
        /// <returns>An <see cref="IActionResult"/> with the update result.</returns>
        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateUserProfile(int userId, [FromBody] UpdateProfileRequest request)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    return NotFound(ApiResponse<object>.Fail("Không tìm thấy người dùng", 404));
                }

                // Update user information
                if (!string.IsNullOrWhiteSpace(request.FullName))
                    user.FullName = request.FullName.Trim();
                
                if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                    user.PhoneNumber = request.PhoneNumber.Trim();

                await _context.SaveChangesAsync();

                var result = new
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    UpdatedAt = DateTime.Now,
                    Message = "Cập nhật thông tin thành công"
                };

                return Ok(ApiResponse<object>.Success(result, "Thông tin đã được cập nhật"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Gets user activity summary for dashboard.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>An <see cref="IActionResult"/> with activity summary.</returns>
        [HttpGet("{userId}/activity")]
        public async Task<IActionResult> GetUserActivity(int userId)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    return NotFound(ApiResponse<object>.Fail("Không tìm thấy người dùng", 404));
                }

                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;
                var last7Days = DateTime.Now.AddDays(-7);
                var last30Days = DateTime.Now.AddDays(-30);

                var activity = new
                {
                    // This Month
                    DocumentsThisMonth = await _context.Documents
                        .CountAsync(d => d.UserId == userId && 
                                    d.CreatedAt.HasValue && 
                                    d.CreatedAt.Value.Month == currentMonth && 
                                    d.CreatedAt.Value.Year == currentYear),
                    
                    CitationsThisMonth = await _context.Citations
                        .CountAsync(c => c.UserId == userId && 
                                    c.CreatedAt.HasValue &&
                                    c.CreatedAt.Value.Month == currentMonth && 
                                    c.CreatedAt.Value.Year == currentYear),

                    // Last 7 Days
                    DocumentsLast7Days = await _context.Documents
                        .CountAsync(d => d.UserId == userId && d.CreatedAt >= last7Days),
                    
                    CitationsLast7Days = await _context.Citations
                        .CountAsync(c => c.UserId == userId && c.CreatedAt.HasValue && c.CreatedAt >= last7Days),

                    // Last 30 Days
                    DocumentsLast30Days = await _context.Documents
                        .CountAsync(d => d.UserId == userId && d.CreatedAt >= last30Days),
                    
                    CitationsLast30Days = await _context.Citations
                        .CountAsync(c => c.UserId == userId && c.CreatedAt.HasValue && c.CreatedAt >= last30Days),

                    // Recent Activity
                    RecentDocuments = await _context.Documents
                        .Where(d => d.UserId == userId)
                        .OrderByDescending(d => d.CreatedAt)
                        .Take(5)
                        .Select(d => new { d.Title, d.CreatedAt, d.DocumentType })
                        .ToListAsync(),

                    RecentCitations = await _context.Citations
                        .Where(c => c.UserId == userId && c.CreatedAt.HasValue)
                        .OrderByDescending(c => c.CreatedAt)
                        .Take(5)
                        .Select(c => new { c.Style, c.CreatedAt })
                        .ToListAsync(),

                    // Statistics
                    TotalActiveDays = await GetActiveDaysCount(userId),
                    FavoriteDocumentType = await _context.Documents
                        .Where(d => d.UserId == userId)
                        .GroupBy(d => d.DocumentType)
                        .OrderByDescending(g => g.Count())
                        .Select(g => new { Type = g.Key.ToString(), Count = g.Count() })
                        .FirstOrDefaultAsync()
                };

                return Ok(ApiResponse<object>.Success(activity, "Thông tin hoạt động được truy xuất"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Uploads and updates user avatar image.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="avatarFile">The avatar image file.</param>
        /// <returns>An <see cref="IActionResult"/> with the upload result.</returns>
        [HttpPost("{userId}/upload-avatar")]
        public async Task<IActionResult> UploadAvatar(int userId, IFormFile avatarFile)
        {
            try
            {
                // Validate user exists
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    return NotFound(ApiResponse<object>.Fail("Không tìm thấy người dùng", 404));
                }

                // Validate file
                if (avatarFile == null || avatarFile.Length == 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn tệp hình ảnh", 400));
                }

                // Check file size (limit to 5MB)
                if (avatarFile.Length > 5 * 1024 * 1024)
                {
                    return BadRequest(ApiResponse<object>.Fail("Kích thước tệp không được vượt quá 5MB", 400));
                }

                // Check file extension
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                var fileExtension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(ApiResponse<object>.Fail("Chỉ hỗ trợ tệp hình ảnh (jpg, jpeg, png, gif, bmp)", 400));
                }

                // Create avatars directory if it doesn't exist
                var avatarsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "avatars");
                if (!Directory.Exists(avatarsDirectory))
                {
                    Directory.CreateDirectory(avatarsDirectory);
                }

                // Generate unique filename
                var fileName = $"avatar_{userId}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                var filePath = Path.Combine(avatarsDirectory, fileName);

                // Delete old avatar file if exists
                if (!string.IsNullOrEmpty(user.AvatarUrl))
                {
                    var oldFileName = Path.GetFileName(user.AvatarUrl);
                    var oldFilePath = Path.Combine(avatarsDirectory, oldFileName);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                        catch (Exception ex)
                        {
                            // Log the error but don't fail the upload
                            Console.WriteLine($"Không thể xóa avatar cũ: {ex.Message}");
                        }
                    }
                }

                // Save the new file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(stream);
                }

                // Update user avatar URL in database
                var avatarUrl = $"/uploads/avatars/{fileName}";
                user.AvatarUrl = avatarUrl;
                await _context.SaveChangesAsync();

                var result = new
                {
                    UserId = userId,
                    AvatarUrl = avatarUrl,
                    FileName = fileName,
                    FileSize = avatarFile.Length,
                    UploadedAt = DateTime.Now,
                    Message = "Tải lên ảnh đại diện thành công"
                };

                return Ok(ApiResponse<object>.Success(result, "Ảnh đại diện đã được cập nhật"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Deletes user avatar image.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>An <see cref="IActionResult"/> with the delete result.</returns>
        [HttpDelete("{userId}/avatar")]
        public async Task<IActionResult> DeleteAvatar(int userId)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    return NotFound(ApiResponse<object>.Fail("Không tìm thấy người dùng", 404));
                }

                if (string.IsNullOrEmpty(user.AvatarUrl))
                {
                    return BadRequest(ApiResponse<object>.Fail("Người dùng chưa có ảnh đại diện", 400));
                }

                // Delete physical file
                var fileName = Path.GetFileName(user.AvatarUrl);
                var avatarsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "avatars");
                var filePath = Path.Combine(avatarsDirectory, fileName);

                if (System.IO.File.Exists(filePath))
                {
                    try
                    {
                        System.IO.File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with database update
                        Console.WriteLine($"Không thể xóa tệp avatar: {ex.Message}");
                    }
                }

                // Update database
                user.AvatarUrl = null;
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<object>.Success(new { UserId = userId, Message = "Đã xóa ảnh đại diện" }, "Ảnh đại diện đã được xóa"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Helper method to calculate the number of active days for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>The number of days with activity.</returns>
        private async Task<int> GetActiveDaysCount(int userId)
        {
            try
            {
                var documentDates = await _context.Documents
                    .Where(d => d.UserId == userId && d.CreatedAt.HasValue)
                    .Select(d => d.CreatedAt!.Value.Date)
                    .Distinct()
                    .ToListAsync();

                var citationDates = await _context.Citations
                    .Where(c => c.UserId == userId && c.CreatedAt.HasValue)
                    .Select(c => c.CreatedAt!.Value.Date)
                    .Distinct()
                    .ToListAsync();

                var allActiveDates = documentDates.Union(citationDates).Distinct();
                return allActiveDates.Count();
            }
            catch
            {
                return 0;
            }
        }

       
    }

    /// <summary>
    /// Request model for updating user profile.
    /// </summary>
    public class UpdateProfileRequest
    {
        /// <summary>
        /// Gets or sets the full name to update.
        /// </summary>
        public string? FullName { get; set; }
        /// <summary>
        /// Gets or sets the phone number to update.
        /// </summary>
        public string? PhoneNumber { get; set; }
    }
}
