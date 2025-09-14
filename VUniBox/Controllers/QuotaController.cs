using Microsoft.AspNetCore.Mvc;
using VUniBox.Models.DTO.Response;
using VUniBox.Services.Quota;

namespace VUniBox.Controllers
{
    /// <summary>
    /// Controller for managing user quota and usage statistics
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class QuotaController : ControllerBase
    {
        private readonly IQuotaManagementService _quotaManagementService;

        public QuotaController(IQuotaManagementService quotaManagementService)
        {
            _quotaManagementService = quotaManagementService;
        }

        /// <summary>
        /// Get current usage statistics and quota information for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Quota usage information</returns>
        [HttpGet("usage/{userId}")]
        public async Task<IActionResult> GetUsageStats(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID người dùng không hợp lệ", 400));
                }

                var usage = await _quotaManagementService.GetUsageStatsAsync(userId);
                
                return Ok(ApiResponse<QuotaUsageResponse>.Success(usage, "Thông tin sử dụng quota được truy xuất thành công"));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Check if user can use citation feature
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Boolean indicating if citation can be used</returns>
        [HttpGet("check/citation/{userId}")]
        public async Task<IActionResult> CheckCitationQuota(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID người dùng không hợp lệ", 400));
                }

                var canUse = await _quotaManagementService.CanUseCitationAsync(userId);
                
                var response = new
                {
                    CanUse = canUse,
                    Message = canUse ? "Có thể sử dụng tính năng trích dẫn" : "Đã vượt quá giới hạn trích dẫn của gói"
                };
                
                return Ok(ApiResponse<object>.Success(response, "Kiểm tra quota trích dẫn thành công"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Check if user can use chatbot feature
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Boolean indicating if chatbot can be used</returns>
        [HttpGet("check/chatbot/{userId}")]
        public async Task<IActionResult> CheckChatbotQuota(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID người dùng không hợp lệ", 400));
                }

                var canUse = await _quotaManagementService.CanUseChatbotAsync(userId);
                
                var response = new
                {
                    CanUse = canUse,
                    Message = canUse ? "Có thể sử dụng chatbot" : "Đã vượt quá giới hạn chatbot của gói"
                };
                
                return Ok(ApiResponse<object>.Success(response, "Kiểm tra quota chatbot thành công"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Check if user can store additional documents
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="sizeMb">Additional storage size in MB</param>
        /// <returns>Boolean indicating if storage can be used</returns>
        [HttpGet("check/storage/{userId}")]
        public async Task<IActionResult> CheckStorageQuota(int userId, [FromQuery] int sizeMb = 0)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID người dùng không hợp lệ", 400));
                }

                var canUse = await _quotaManagementService.CanStoreDocumentAsync(userId, sizeMb);
                
                var response = new
                {
                    CanUse = canUse,
                    Message = canUse ? "Có thể lưu trữ tài liệu" : "Đã vượt quá giới hạn lưu trữ của gói",
                    RequiredSizeMb = sizeMb
                };
                
                return Ok(ApiResponse<object>.Success(response, "Kiểm tra quota lưu trữ thành công"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get quota summary for dashboard
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Summary of quota usage</returns>
        [HttpGet("summary/{userId}")]
        public async Task<IActionResult> GetQuotaSummary(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID người dùng không hợp lệ", 400));
                }

                var usage = await _quotaManagementService.GetUsageStatsAsync(userId);
                
                var summary = new
                {
                    PlanName = usage.PlanName,
                    Usage = new
                    {
                        Storage = new
                        {
                            Used = usage.StorageUsedMb,
                            Limit = usage.StorageLimitMb,
                            Remaining = usage.StorageRemainingMb,
                            UsagePercent = Math.Round(usage.StorageUsagePercent, 1)
                        },
                        Citation = new
                        {
                            Used = usage.CitationUsed,
                            Limit = usage.CitationLimit,
                            Remaining = usage.CitationRemaining,
                            UsagePercent = Math.Round(usage.CitationUsagePercent, 1)
                        },
                        Chatbot = new
                        {
                            Used = usage.ChatbotUsed,
                            Limit = usage.ChatbotLimit,
                            Remaining = usage.ChatbotRemaining,
                            UsagePercent = Math.Round(usage.ChatbotUsagePercent, 1)
                        }
                    },
                    LastUpdated = usage.LastUpdated
                };
                
                return Ok(ApiResponse<object>.Success(summary, "Tóm tắt quota được truy xuất thành công"));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }
    }
}
