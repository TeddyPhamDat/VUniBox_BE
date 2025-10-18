using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;
using VUniBox.Services.Citation;
using VUniBox.Services.Quota;

namespace VUniBox.Controllers
{
    [ApiController]
    [Route("api/citation")]
    public class CitationController : ControllerBase
    {
        private readonly ICitationManagementService _citationManagementService;
        private readonly IQuotaManagementService _quotaManagementService;

        public CitationController(
            ICitationManagementService citationManagementService,
            IQuotaManagementService quotaManagementService)
        {
            _citationManagementService = citationManagementService;
            _quotaManagementService = quotaManagementService;
        }

        /// <summary>
        /// Generate citation for a document in specified style
        /// </summary>
        /// <param name="request">Citation generation request</param>
        /// <returns>Generated citation</returns>
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateCitation([FromBody] CitationGenerateRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Yêu cầu không hợp lệ", 400));
                }

                if (request.UserId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID người dùng không hợp lệ", 400));
                }

                if (request.DocumentId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID tài liệu không hợp lệ", 400));
                }

                if (string.IsNullOrWhiteSpace(request.CitationStyle))
                {
                    return BadRequest(ApiResponse<object>.Fail("Phong cách trích dẫn không được để trống", 400));
                }

                // Check quota before generating citation
                var canUseCitation = await _quotaManagementService.CanUseCitationAsync(request.UserId);
                if (!canUseCitation)
                {
                    return BadRequest(ApiResponse<object>.Fail(
                        "Bạn đã vượt quá giới hạn trích dẫn của gói hiện tại. Vui lòng nâng cấp gói để tiếp tục sử dụng.", 429));
                }

                // Validate citation style
                var validStyles = new[] { "APA", "MLA", "Chicago", "Harvard", "IEEE", "Vancouver" };
                if (!validStyles.Contains(request.CitationStyle, StringComparer.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<object>.Fail(
                        $"Phong cách trích dẫn '{request.CitationStyle}' không được hỗ trợ. " +
                        $"Các phong cách hỗ trợ: {string.Join(", ", validStyles)}", 400));
                }

                Console.WriteLine($"[CitationController] Generating citation for document {request.DocumentId} in {request.CitationStyle} style");

                var citation = await _citationManagementService.GenerateCitationAsync(
                    request.DocumentId, 
                    request.CitationStyle);

                if (citation == null)
                {
                    return NotFound(ApiResponse<object>.Fail(
                        "Không tìm thấy tài liệu hoặc không thể tạo trích dẫn", 404));
                }

                // Increment citation usage after successful generation
                await _quotaManagementService.IncrementCitationUsageAsync(request.UserId);

                Console.WriteLine($"[CitationController] Successfully generated citation for document {request.DocumentId}");

                return Ok(ApiResponse<CitationResponse>.Success(citation, "Trích dẫn đã được tạo thành công"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CitationController] Error generating citation: {ex.Message}");
                
                return StatusCode(500, ApiResponse<object>.Fail(
                    "Lỗi hệ thống khi tạo trích dẫn. Vui lòng thử lại sau.", 500));
            }
        }

        /// <summary>
        /// Regenerate citation for a document with new style
        /// </summary>
        /// <param name="request">Citation regeneration request</param>
        /// <returns>Regenerated citation</returns>
        [HttpPost("regenerate")]
        public async Task<IActionResult> RegenerateCitation([FromBody] CitationRegenerateRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Yêu cầu không hợp lệ", 400));
                }

                if (request.UserId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID người dùng không hợp lệ", 400));
                }

                if (request.DocumentId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID tài liệu không hợp lệ", 400));
                }

                if (string.IsNullOrWhiteSpace(request.NewCitationStyle))
                {
                    return BadRequest(ApiResponse<object>.Fail("Phong cách trích dẫn mới không được để trống", 400));
                }

                // Check quota before regenerating citation
                var canUseCitation = await _quotaManagementService.CanUseCitationAsync(request.UserId);
                if (!canUseCitation)
                {
                    return BadRequest(ApiResponse<object>.Fail(
                        "Bạn đã vượt quá giới hạn trích dẫn của gói hiện tại. Vui lòng nâng cấp gói để tiếp tục sử dụng.", 429));
                }

                // Validate citation style
                var validStyles = new[] { "APA", "MLA", "Chicago", "Harvard", "IEEE", "Vancouver" };
                if (!validStyles.Contains(request.NewCitationStyle, StringComparer.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<object>.Fail(
                        $"Phong cách trích dẫn '{request.NewCitationStyle}' không được hỗ trợ. " +
                        $"Các phong cách hỗ trợ: {string.Join(", ", validStyles)}", 400));
                }

                Console.WriteLine($"[CitationController] Regenerating citation for document {request.DocumentId} with {request.NewCitationStyle} style");

                var citation = await _citationManagementService.RegenerateCitationAsync(
                    request.DocumentId, 
                    request.NewCitationStyle);

                if (citation == null)
                {
                    return NotFound(ApiResponse<object>.Fail(
                        "Không tìm thấy tài liệu hoặc không thể tạo lại trích dẫn", 404));
                }

                // Increment citation usage after successful regeneration
                await _quotaManagementService.IncrementCitationUsageAsync(request.UserId);

                Console.WriteLine($"[CitationController] Successfully regenerated citation for document {request.DocumentId}");

                return Ok(ApiResponse<CitationResponse>.Success(citation, "Trích dẫn đã được tạo lại thành công"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CitationController] Error regenerating citation: {ex.Message}");
                
                return StatusCode(500, ApiResponse<object>.Fail(
                    "Lỗi hệ thống khi tạo lại trích dẫn. Vui lòng thử lại sau.", 500));
            }
        }

        /// <summary>
        /// Get available citation styles
        /// </summary>
        /// <returns>List of supported citation styles</returns>
        [HttpGet("styles")]
        public IActionResult GetCitationStyles()
        {
            var styles = new[]
            {
                new { 
                    Style = "APA", 
                    Name = "American Psychological Association", 
                    Description = "Commonly used in psychology, education, and social sciences" 
                },
                new { 
                    Style = "MLA", 
                    Name = "Modern Language Association", 
                    Description = "Commonly used in literature, arts, and humanities" 
                },
                new { 
                    Style = "Chicago", 
                    Name = "Chicago Manual of Style", 
                    Description = "Commonly used in history, literature, and the arts" 
                },
                new { 
                    Style = "Harvard", 
                    Name = "Harvard Referencing", 
                    Description = "Commonly used in sciences and social sciences" 
                },
                new { 
                    Style = "IEEE", 
                    Name = "Institute of Electrical and Electronics Engineers", 
                    Description = "Commonly used in engineering and technology" 
                },
                new { 
                    Style = "Vancouver", 
                    Name = "Vancouver System", 
                    Description = "Commonly used in medicine and life sciences" 
                }
            };

            return Ok(ApiResponse<object>.Success(styles, "Danh sách phong cách trích dẫn được hỗ trợ"));
        }

        /// <summary>
        /// Get all citations for a user (simplified view with essential information)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of simplified citations</returns>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserCitations(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID người dùng không hợp lệ", 400));
                }

                var citations = await _citationManagementService.GetUserCitationsSimplifiedAsync(userId);
                return Ok(ApiResponse<object>.Success(citations, "Danh sách trích dẫn của người dùng"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi server: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get all citations for a user sorted by author name (A-Z) - simplified view
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of simplified citations sorted by author</returns>
        [HttpGet("user/{userId}/sorted-by-author")]
        public async Task<IActionResult> GetUserCitationsSortedByAuthor(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID người dùng không hợp lệ", 400));
                }

                var citations = await _citationManagementService.GetUserCitationsSimplifiedSortedByAuthorAsync(userId);
                return Ok(ApiResponse<object>.Success(citations, "Danh sách trích dẫn được sắp xếp theo tác giả từ A-Z"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi server: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get all citations for a user with full metadata (admin/debug endpoint)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of citations with full metadata</returns>
        [HttpGet("user/{userId}/full-metadata")]
        public async Task<IActionResult> GetUserCitationsFullMetadata(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("ID người dùng không hợp lệ", 400));
                }

                var citations = await _citationManagementService.GetUserCitationsAsync(userId);
                return Ok(ApiResponse<object>.Success(citations, "Danh sách trích dẫn với metadata đầy đủ"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi server: {ex.Message}", 500));
            }
        }
    }
}
