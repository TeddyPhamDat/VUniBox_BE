using Microsoft.AspNetCore.Mvc;
using VUniBox.Models.DTO.Response;
using VUniBox.Services.DocumentManagement;
using VUniBox.Services.Quota;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminController"/> class.
        /// </summary>
        /// <param name="documentLifecycleService">The document lifecycle service.</param>
        /// <param name="quotaManagementService">The quota management service.</param>
        public AdminController(
            IDocumentLifecycleService documentLifecycleService,
            IQuotaManagementService quotaManagementService)
        {
            _documentLifecycleService = documentLifecycleService;
            _quotaManagementService = quotaManagementService;
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
                    return Ok(ApiResponse<object>.Success(new { 
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
    }
}
