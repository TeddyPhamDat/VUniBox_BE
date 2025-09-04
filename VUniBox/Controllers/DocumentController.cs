using Microsoft.AspNetCore.Mvc;
using VUniBox.Models;
using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;
using VUniBox.Services.DocumentManagement;

namespace VUniBox.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentLifecycleService _documentLifecycleService;

        public DocumentController(IDocumentLifecycleService documentLifecycleService)
        {
            _documentLifecycleService = documentLifecycleService;
        }

        /// <summary>
        /// Save document to folder or move to trash based on user choice
        /// </summary>
        /// <param name="request">Document save request</param>
        /// <returns>Save result</returns>
        [HttpPost("save")]
        public async Task<IActionResult> SaveDocument([FromBody] DocumentSaveRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Request body is required", 400));
                }

                Documents document;
                bool isInTrash = false;
                DateTime? expiryDate = null;

                if (request.SaveToFolder)
                {
                    // Lưu vào folder theo type
                    document = await _documentLifecycleService.SaveDocumentAsync(
                        request.UserId,
                        request.Metadata,
                        request.DocumentType, 
                        request.FilePath);
                }
                else
                {
                    // Tạm thời lưu document với status Processing
                    document = await _documentLifecycleService.SaveDocumentAsync(
                        request.UserId, 
                        request.Metadata, 
                        request.DocumentType, 
                        request.FilePath);

                    // Sau đó chuyển vào thùng rác
                    await _documentLifecycleService.MoveToTrashAsync(document.DocumentId, request.UserId);
                    isInTrash = true;
                    expiryDate = DateTime.UtcNow.AddDays(10);
                }

                var response = new DocumentSaveResponse
                {
                    Success = true,
                    Document = new DocumentDto(document), // Convert to DocumentDto
                    Message = request.SaveToFolder ? "Document saved successfully" : "Document moved to trash",
                    IsInTrash = isInTrash,
                    ExpiryDate = expiryDate
                };

                return Ok(ApiResponse<DocumentSaveResponse>.Success(response, "Document processed successfully"));
            }
            catch (Exception ex)
            {
                var errorResponse = new DocumentSaveResponse
                {
                    Success = false,
                    Message = "Failed to save document",
                    Error = ex.Message
                };

                return StatusCode(500, ApiResponse<DocumentSaveResponse>.Fail($"Internal server error: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Move document to trash
        /// </summary>
        /// <param name="request">Document trash request</param>
        /// <returns>Trash result</returns>
        [HttpPost("trash")]
        public async Task<IActionResult> MoveToTrash([FromBody] DocumentTrashRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Request body is required", 400));
                }

                var success = await _documentLifecycleService.MoveToTrashAsync(request.DocumentId, request.UserId);

                if (success)
                {
                    return Ok(ApiResponse<object>.Success(new { 
                        DocumentId = request.DocumentId,
                        ExpiryDate = DateTime.UtcNow.AddDays(10),
                        Message = "Document moved to trash successfully"
                    }, "Document moved to trash"));
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Fail("Document not found or already in trash", 400));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Restore document from trash
        /// </summary>
        /// <param name="request">Document trash request</param>
        /// <returns>Restore result</returns>
        [HttpPost("restore")]
        public async Task<IActionResult> RestoreFromTrash([FromBody] DocumentTrashRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Request body is required", 400));
                }

                var success = await _documentLifecycleService.RestoreFromTrashAsync(request.DocumentId, request.UserId);

                if (success)
                {
                    return Ok(ApiResponse<object>.Success(new { 
                        DocumentId = request.DocumentId,
                        Message = "Document restored from trash successfully"
                    }, "Document restored from trash"));
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Fail("Document not found in trash", 400));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Delete document permanently
        /// </summary>
        /// <param name="request">Document trash request</param>
        /// <returns>Delete result</returns>
        [HttpDelete("permanent")]
        public async Task<IActionResult> DeletePermanently([FromBody] DocumentTrashRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Request body is required", 400));
                }

                var success = await _documentLifecycleService.DeletePermanentlyAsync(request.DocumentId, request.UserId);

                if (success)
                {
                    return Ok(ApiResponse<object>.Success(new { 
                        DocumentId = request.DocumentId,
                        Message = "Document deleted permanently"
                    }, "Document deleted permanently"));
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Fail("Document not found", 400));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get all documents in trash for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Trash documents</returns>
        [HttpGet("trash/{userId}")]
        public async Task<IActionResult> GetTrashDocuments(int userId)
        {
            try
            {
                var trashDocuments = await _documentLifecycleService.GetTrashDocumentsAsync(userId);

                var response = new TrashResponse
                {
                    Success = true,
                    TrashDocuments = trashDocuments,
                    Message = "Trash documents retrieved successfully",
                    TotalCount = trashDocuments.Count
                };

                return Ok(ApiResponse<TrashResponse>.Success(response, "Trash documents retrieved"));
            }
            catch (Exception ex)
            {
                var errorResponse = new TrashResponse
                {
                    Success = false,
                    Message = "Failed to get trash documents",
                    Error = ex.Message
                };

                return StatusCode(500, ApiResponse<TrashResponse>.Fail($"Internal server error: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Clean expired documents from trash (Admin only)
        /// </summary>
        /// <returns>Clean result</returns>
        [HttpPost("clean-trash")]
        public async Task<IActionResult> CleanExpiredTrash()
        {
            try
            {
                var success = await _documentLifecycleService.CleanExpiredTrashAsync();

                if (success)
                {
                    return Ok(ApiResponse<object>.Success(new { 
                        Message = "Expired trash documents cleaned successfully"
                    }, "Trash cleaned"));
                }
                else
                {
                    return StatusCode(500, ApiResponse<object>.Fail("Failed to clean trash", 500));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}", 500));
            }
        }
    }
}

