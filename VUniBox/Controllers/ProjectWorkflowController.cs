using Microsoft.AspNetCore.Mvc;
using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.DTO;
using VUniBox.Services.Classification;
using VUniBox.Services.Metadata;
using VUniBox.Services.DocumentManagement;
using VUniBox.Models.Enum;

namespace VUniBox.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectWorkflowController : ControllerBase
    {
        private readonly IClassificationService _classificationService;
        private readonly IMetadataExtractionService _metadataExtractionService;
        private readonly IDocumentLifecycleService _documentLifecycleService;
        private readonly IConfiguration _configuration;
        private readonly string _uploadPath;

        public ProjectWorkflowController(
            IClassificationService classificationService,
            IMetadataExtractionService metadataExtractionService,
            IDocumentLifecycleService documentLifecycleService,
            IConfiguration configuration)
        {
            _classificationService = classificationService;
            _metadataExtractionService = metadataExtractionService;
            _documentLifecycleService = documentLifecycleService;
            _configuration = configuration;
            _uploadPath = _configuration["FileUpload:Path"] ?? "uploads";
        }

        /// <summary>
        /// Step 1: Upload file and auto-classify for confirmation
        /// </summary>
        /// <param name="file">File to upload</param>
        /// <returns>Classification result for user confirmation</returns>
        [HttpPost("upload-file")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(ApiResponse<object>.Fail("Không có file được tải lên", 400));
                }

                // 1. VALIDATE FILE
                var maxFileSize = _configuration.GetValue<long>("FileUpload:MaxFileSize", 10485760);
                if (file.Length > maxFileSize)
                {
                    return BadRequest(ApiResponse<object>.Fail($"Kích thước file vượt quá {maxFileSize / 1024 / 1024}MB", 400));
                }

                var allowedExtensions = _configuration.GetSection("FileUpload:AllowedExtensions").Get<string[]>() 
                    ?? new[] { ".pdf", ".doc", ".docx", ".txt", ".md" };
                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(ApiResponse<object>.Fail($"Loại file {fileExtension} không được hỗ trợ", 400));
                }

                // 2. UPLOAD FILE
                if (!Directory.Exists(_uploadPath))
                {
                    Directory.CreateDirectory(_uploadPath);
                }

                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(_uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 3. AUTO CLASSIFICATION
                var classificationRequest = new ClassificationRequest
                {
                    InputType = InputType.File,
                    FileName = file.FileName
                };

                var classificationResult = await _classificationService.ClassifyAsync(classificationRequest);

                if (!classificationResult.Success)
                {
                    return BadRequest(ApiResponse<object>.Fail($"Không thể nhận diện loại tài liệu: {classificationResult.Message}", 400));
                }

                // 4. RETURN CONFIRMATION DATA FOR UI
                var response = new FileUploadResponse
                {
                    Success = true,
                    FilePath = filePath,
                    OriginalFileName = file.FileName,
                    FileSize = file.Length,
                    DetectedType = classificationResult.DocumentType,
                    TypeName = GetDocumentTypeName(classificationResult.DocumentType),
                    ConfirmationMessage = $"Hệ thống nhận diện đây là tài liệu loại: {GetDocumentTypeName(classificationResult.DocumentType)}",
                    Question = "Bạn có muốn lưu file này không?",
                    Subtitle = "Bạn có thể lưu hoặc không lưu file trong thao tác",
                    TempId = Guid.NewGuid().ToString()
                };

                return Ok(ApiResponse<FileUploadResponse>.Success(response, "File đã được tải lên và nhận diện thành công"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Step 1: Process URL and auto-classify for confirmation
        /// </summary>
        /// <param name="request">URL processing request</param>
        /// <returns>Classification result for user confirmation</returns>
        [HttpPost("process-url")]
        public async Task<IActionResult> ProcessUrl([FromBody] UrlInputRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Url))
                {
                    return BadRequest(ApiResponse<object>.Fail("URL không được để trống", 400));
                }

                // 1. AUTO CLASSIFICATION
                var classificationRequest = new ClassificationRequest
                {
                    InputType = InputType.Url,
                    Url = request.Url
                };

                var classificationResult = await _classificationService.ClassifyAsync(classificationRequest);

                if (!classificationResult.Success)
                {
                    return BadRequest(ApiResponse<object>.Fail($"Không thể nhận diện loại tài liệu: {classificationResult.Message}", 400));
                }

                // 2. RETURN CONFIRMATION DATA FOR UI
                var response = new UrlProcessResponse
                {
                    Success = true,
                    Url = request.Url,
                    DetectedType = classificationResult.DocumentType,
                    TypeName = GetDocumentTypeName(classificationResult.DocumentType),
                    ConfirmationMessage = $"Hệ thống nhận diện đây là tài liệu loại: {GetDocumentTypeName(classificationResult.DocumentType)}",
                    Question = "Bạn có muốn lưu URL này không?",
                    Subtitle = "Bạn có thể lưu hoặc không lưu URL trong thao tác",
                    TempId = Guid.NewGuid().ToString()
                };

                return Ok(ApiResponse<UrlProcessResponse>.Success(response, "URL đã được phân tích và nhận diện thành công"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Step 2: User confirms save/trash decision
        /// </summary>
        /// <param name="request">User confirmation request</param>
        /// <returns>Final processing result</returns>
        [HttpPost("confirm-action")]
        public async Task<IActionResult> ConfirmAction([FromBody] UserConfirmationRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Yêu cầu không hợp lệ", 400));
                }

                // Extract metadata based on input type
                DocumentMetadataDto metadata;
                if (!string.IsNullOrEmpty(request.FilePath))
                {
                    // File processing
                    metadata = await _metadataExtractionService.ExtractFromFileAsync(
                        request.FilePath, 
                        request.FileName ?? Path.GetFileName(request.FilePath), 
                        request.DocumentType);
                }
                else if (!string.IsNullOrEmpty(request.Url))
                {
                    // URL processing
                    metadata = await _metadataExtractionService.ExtractFromUrlAsync(
                        request.Url, 
                        request.DocumentType);
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Fail("Thiếu thông tin file hoặc URL", 400));
                }

                // Save document
                var document = await _documentLifecycleService.SaveDocumentAsync(
                    request.UserId,
                    metadata,
                    request.DocumentType,
                    request.FilePath);

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

                var response = new ActionConfirmationResponse
                {
                    Success = true,
                    Document = new DocumentDto(document), // Convert to DTO
                    Action = action,
                    Message = request.SaveToFolder 
                        ? $"Tài liệu đã được lưu vào {location}" 
                        : $"Tài liệu đã được chuyển vào {location} (tự động xóa sau 10 ngày)",
                    IsInTrash = isInTrash,
                    ExpiryDate = expiryDate,
                    Location = location,
                    Metadata = metadata
                };

                return Ok(ApiResponse<ActionConfirmationResponse>.Success(response, "Xử lý thành công"));
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
}