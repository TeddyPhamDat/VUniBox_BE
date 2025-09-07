using Microsoft.AspNetCore.Mvc;
using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;
using VUniBox.Services.Classification;
using VUniBox.Models.Enum;

namespace VUniBox.Controllers
{
    /// <summary>
    /// Controller for document upload and automatic classification
    /// Handles file upload and URL processing with auto-classification
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentClassificationController : ControllerBase
    {
        private readonly IClassificationService _classificationService;
        private readonly IConfiguration _configuration;
        private readonly string _uploadPath;

        public DocumentClassificationController(
            IClassificationService classificationService,
            IConfiguration configuration)
        {
            _classificationService = classificationService;
            _configuration = configuration;
            _uploadPath = _configuration["FileUpload:Path"] ?? "uploads";
        }

        /// <summary>
        /// Upload file and auto-classify document type
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

                // 4. RETURN CLASSIFICATION RESULT FOR UI CONFIRMATION
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
        /// Process URL and auto-classify document type
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

                // 2. RETURN CLASSIFICATION RESULT FOR UI CONFIRMATION
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