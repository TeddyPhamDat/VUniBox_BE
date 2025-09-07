using Microsoft.AspNetCore.Mvc;
using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.DTO;
using VUniBox.Services.Metadata;
using VUniBox.Services.DocumentManagement;
using VUniBox.Models.Enum;

namespace VUniBox.Controllers
{
    /// <summary>
    /// Controller for document metadata extraction and processing
    /// Handles metadata extraction, document saving, and citation generation
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentMetadataController : ControllerBase
    {
        private readonly IMetadataExtractionService _metadataExtractionService;
        private readonly IDocumentLifecycleService _documentLifecycleService;

        public DocumentMetadataController(
            IMetadataExtractionService metadataExtractionService,
            IDocumentLifecycleService documentLifecycleService)
        {
            _metadataExtractionService = metadataExtractionService;
            _documentLifecycleService = documentLifecycleService;
        }

        /// <summary>
        /// Extract metadata and save document based on user confirmation
        /// This is the final step after classification confirmation
        /// </summary>
        /// <param name="request">User confirmation request with document details</param>
        /// <returns>Complete metadata extraction and document save result</returns>
        [HttpPost("extract-and-save")]
        public async Task<IActionResult> ExtractMetadataAndSave([FromBody] UserConfirmationRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Yêu cầu không hợp lệ", 400));
                }

                // 1. EXTRACT METADATA based on input type
                DocumentMetadataDto metadata;
                if (!string.IsNullOrEmpty(request.FilePath))
                {
                    // File processing - extract metadata from uploaded file
                    metadata = await _metadataExtractionService.ExtractFromFileAsync(
                        request.FilePath, 
                        request.FileName ?? Path.GetFileName(request.FilePath), 
                        request.DocumentType);
                }
                else if (!string.IsNullOrEmpty(request.Url))
                {
                    // URL processing - extract metadata from web page
                    metadata = await _metadataExtractionService.ExtractFromUrlAsync(
                        request.Url, 
                        request.DocumentType);
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Fail("Thiếu thông tin file hoặc URL", 400));
                }

                // 2. SAVE DOCUMENT with extracted metadata
                var document = await _documentLifecycleService.SaveDocumentAsync(
                    request.UserId,
                    metadata,
                    request.DocumentType,
                    request.FilePath);

                // 3. HANDLE USER DECISION (Save to folder or Move to trash)
                bool isInTrash = false;
                DateTime? expiryDate = null;
                string location = "";
                string action = "";

                if (request.SaveToFolder)
                {
                    // User clicked "Lưu" - save to folder
                    location = $"Thư mục {GetDocumentTypeName(request.DocumentType)}";
                    action = "saved";
                }
                else
                {
                    // User clicked "Không Lưu" - move to trash
                    await _documentLifecycleService.MoveToTrashAsync(document.DocumentId, request.UserId);
                    isInTrash = true;
                    expiryDate = DateTime.UtcNow.AddDays(10);
                    location = "Thùng rác";
                    action = "moved_to_trash";
                }

                // 4. RETURN COMPLETE METADATA AND DOCUMENT INFO
                var response = new ActionConfirmationResponse
                {
                    Success = true,
                    DocumentId = document.DocumentId,
                    Action = action,
                    Message = request.SaveToFolder 
                        ? $"Tài liệu đã được lưu vào {location}" 
                        : $"Tài liệu đã được chuyển vào {location} (tự động xóa sau 10 ngày)",
                    IsInTrash = isInTrash,
                    ExpiryDate = expiryDate,
                    Location = location,
                    Metadata = new CitationMetadataDto(document, metadata) // Complete metadata for citation
                };

                return Ok(ApiResponse<ActionConfirmationResponse>.Success(response, "Xử lý metadata thành công"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Extract metadata only (without saving document)
        /// Useful for preview or testing metadata extraction
        /// </summary>
        /// <param name="request">Metadata extraction request</param>
        /// <returns>Extracted metadata only</returns>
        [HttpPost("extract-metadata")]
        public async Task<IActionResult> ExtractMetadata([FromBody] MetadataExtractionRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Yêu cầu không hợp lệ", 400));
                }

                DocumentMetadataDto metadata;
                
                if (!string.IsNullOrEmpty(request.FilePath))
                {
                    // Extract from file
                    metadata = await _metadataExtractionService.ExtractFromFileAsync(
                        request.FilePath, 
                        request.FileName ?? Path.GetFileName(request.FilePath), 
                        request.DocumentType);
                }
                else if (!string.IsNullOrEmpty(request.Url))
                {
                    // Extract from URL
                    metadata = await _metadataExtractionService.ExtractFromUrlAsync(
                        request.Url, 
                        request.DocumentType);
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Fail("Thiếu thông tin file hoặc URL", 400));
                }

                return Ok(ApiResponse<DocumentMetadataDto>.Success(metadata, "Trích xuất metadata thành công"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get document metadata by document ID
        /// Useful for retrieving metadata of already saved documents
        /// </summary>
        /// <param name="documentId">Document ID</param>
        /// <param name="userId">User ID for security check</param>
        /// <returns>Document metadata</returns>
        [HttpGet("metadata/{documentId}")]
        public async Task<IActionResult> GetDocumentMetadata(int documentId, [FromQuery] int userId)
        {
            try
            {
                // This would need to be implemented in DocumentLifecycleService
                // For now, return a placeholder response
                return Ok(ApiResponse<object>.Success(
                    new { message = "Get metadata by document ID - To be implemented" }, 
                    "Placeholder endpoint"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get Vietnamese name for document type
        /// </summary>
        private string GetDocumentTypeName(DocumentType documentType)
        {
            return documentType switch
            {
                DocumentType.Word => "Word",
                DocumentType.Pdf => "PDF",
                DocumentType.Book => "Sách",
                DocumentType.Research => "Nghiên cứu",
                DocumentType.Newspaper => "Báo/Tạp chí",
                DocumentType.Others => "Tài liệu khác",
                _ => "Không xác định"
            };
        }
    }

    /// <summary>
    /// Request DTO for metadata extraction only
    /// </summary>
    public class MetadataExtractionRequest
    {
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public string? Url { get; set; }
        public DocumentType DocumentType { get; set; }
    }
}