using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using VUniBox.Models.DTO.Response;
using VUniBox.Services.DocumentManagement;
using VUniBox.Services.Quota;
using VUniBox.Services.Admin;
using System;

namespace VUniBox.Controllers
{
    [ApiController]
    [Route("api/admin")]
    /// <summary>
    /// Master controller for managing all administrative tasks within the VUniBox application.
    /// </summary>
    public class AdminController : ControllerBase
    {
        private readonly IDocumentLifecycleService _documentLifecycleService;
        private readonly IQuotaManagementService _quotaManagementService;
        private readonly IAdminDashboardService _adminDashboardService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminController"/> class.
        /// </summary>
        /// <param name="documentLifecycleService">The document lifecycle service.</param>
        /// <param name="quotaManagementService">The quota management service.</param>
        /// <param name="adminDashboardService">The admin dashboard service.</param>
        public AdminController(
            IDocumentLifecycleService documentLifecycleService,
            IQuotaManagementService quotaManagementService,
            IAdminDashboardService adminDashboardService)
        {
            _documentLifecycleService = documentLifecycleService;
            _quotaManagementService = quotaManagementService;
            _adminDashboardService = adminDashboardService;
        }

        /// <summary>
        /// Cleans up expired documents from the trash (Admin only).
        /// </summary>
        /// <returns>An <see cref="IActionResult"/> indicating the cleanup result.</returns>
        [HttpPost("documents/clean-trash")]
        public async Task<IActionResult> CleanExpiredTrash()
        {
            try
            {
                var success = await _documentLifecycleService.CleanExpiredTrashAsync();

                if (success)
                {
                    return Ok(ApiResponse<object>.Success(new
                    {
                        Message = "Tài liệu hết hạn trong thùng rác đã được dọn dẹp thành công"
                    }, "Thùng rác đã được dọn dẹp"));
                }
                else
                {
                    return StatusCode(500, ApiResponse<object>.Fail("Không thể dọn dẹp thùng rác", 500));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Reset monthly usage for a user (Admin only)
        /// </summary>
        /// <param name="userId">User ID (optional, if not provided resets for all users)</param>
        /// <returns>Success status</returns>
        [HttpPost("quota/reset-monthly")]
        public async Task<IActionResult> ResetMonthlyUsage([FromQuery] int? userId = null)
        {
            try
            {
                var success = await _quotaManagementService.ResetMonthlyUsageAsync(userId);

                if (success)
                {
                    var message = userId.HasValue
                        ? $"Đã reset usage tháng cho user {userId}"
                        : "Đã reset usage tháng cho tất cả users";

                    return Ok(ApiResponse<object>.Success(new { Success = true }, message));
                }
                else
                {
                    return StatusCode(500, ApiResponse<object>.Fail("Không thể reset monthly usage", 500));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get comprehensive dashboard data for admin overview
        /// </summary>
        /// <returns>Dashboard data including stats, charts, and recent activities</returns>
        [HttpGet("dashboard")]

        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                var dashboardData = await _adminDashboardService.GetDashboardDataAsync();
                return Ok(ApiResponse<AdminDashboardResponse>.Success(
                    dashboardData,
                    "Dashboard data retrieved successfully"
                ));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get user registration statistics for a specific date range
        /// </summary>
        /// <param name="fromDate">Start date for statistics</param>
        /// <param name="toDate">End date for statistics</param>
        /// <returns>User registration statistics</returns>
        [HttpGet("dashboard/user-stats")]
       
        public async Task<IActionResult> GetUserRegistrationStats(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate)
        {
            try
            {
                var stats = await _adminDashboardService.GetUserRegistrationStatsAsync(fromDate, toDate);
                return Ok(ApiResponse<List<UserRegistrationStatsDto>>.Success(
                    stats,
                    "User registration stats retrieved successfully"
                ));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get document upload statistics for a specific date range
        /// </summary>
        /// <param name="fromDate">Start date for statistics</param>
        /// <param name="toDate">End date for statistics</param>
        /// <returns>Document upload statistics</returns>
        [HttpGet("dashboard/document-stats")]
  
        public async Task<IActionResult> GetDocumentUploadStats(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate)
        {
            try
            {
                var stats = await _adminDashboardService.GetDocumentUploadStatsAsync(fromDate, toDate);
                return Ok(ApiResponse<List<DocumentUploadStatsDto>>.Success(
                    stats,
                    "Document upload stats retrieved successfully"
                ));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get plan usage statistics
        /// </summary>
        /// <returns>Plan usage statistics</returns>
        [HttpGet("dashboard/plan-stats")]
      
        public async Task<IActionResult> GetPlanUsageStats()
        {
            try
            {
                var stats = await _adminDashboardService.GetPlanUsageStatsAsync();
                return Ok(ApiResponse<List<PlanUsageStatsDto>>.Success(
                    stats,
                    "Plan usage stats retrieved successfully"
                ));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get recent activities in the system
        /// </summary>
        /// <param name="limit">Number of activities to retrieve (default: 10)</param>
        /// <returns>Recent activities</returns>
        [HttpGet("dashboard/recent-activities")]
      
        public async Task<IActionResult> GetRecentActivities([FromQuery] int limit = 10)
        {
            try
            {
                var activities = await _adminDashboardService.GetRecentActivitiesAsync(limit);
                return Ok(ApiResponse<List<RecentActivityDto>>.Success(
                    activities,
                    "Recent activities retrieved successfully"
                ));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get revenue statistics for a specific date range
        /// </summary>
        /// <param name="fromDate">Start date for statistics</param>
        /// <param name="toDate">End date for statistics</param>
        /// <returns>Revenue statistics</returns>
        [HttpGet("dashboard/revenue-stats")]
      
        public async Task<IActionResult> GetRevenueStats(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate)
        {
            try
            {
                var stats = await _adminDashboardService.GetRevenueStatsAsync(fromDate, toDate);
                return Ok(ApiResponse<List<RevenueStatsDto>>.Success(
                    stats,
                    "Revenue stats retrieved successfully"
                ));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }
    }
}