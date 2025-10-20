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
    [Route("api/documentClassification")]
    public class DocumentClassificationController : ControllerBase
    {
        private readonly IClassificationService _classificationService;
        private readonly IConfiguration _configuration;
        private readonly string _uploadPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentClassificationController"/> class.
        /// </summary>
        /// <param name="classificationService">The classification service.</param>
        /// <param name="configuration">The application configuration.</param>
        public DocumentClassificationController(
            IClassificationService classificationService,
            IConfiguration configuration)
        {
            _classificationService = classificationService;
            _configuration = configuration;

            // Use absolute path for uploads in production
            var configPath = _configuration["FileUpload:Path"];
            if (!string.IsNullOrEmpty(configPath) && Path.IsPathRooted(configPath))
            {
                _uploadPath = configPath;
            }
            else
            {
                // Default to uploads folder in the app directory
                _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            }
            // Ensure upload path exists
            if (!Directory.Exists(_uploadPath))
            {
                Directory.CreateDirectory(_uploadPath);
            }
        }

        /// <summary>
        /// Handles preflight CORS requests for file upload
        /// </summary>
        [HttpOptions("upload-file")]
        public IActionResult UploadFileOptions()
        {
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            Response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
            Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
            return Ok();
        }


        /// <summary>
        /// Uploads a file and automatically classifies its document type.
        /// </summary>
        /// <param name="file">The file to upload.</param>
        /// <returns>A <see cref="FileUploadResponse"/> containing the classification result for user confirmation.</returns>
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

                // 2. SAVE FILE TO TEMPORARY LOCATION FOR CLASSIFICATION
                var tempPath = Path.Combine(_uploadPath, "temp");
                if (!Directory.Exists(tempPath))
                {
                    Directory.CreateDirectory(tempPath);
                }

                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var tempFilePath = Path.Combine(tempPath, fileName);

                using (var stream = new FileStream(tempFilePath, FileMode.Create))
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
                    // Clean up temp file if classification fails
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                    return BadRequest(ApiResponse<object>.Fail($"Không thể nhận diện loại tài liệu: {classificationResult.Message}", 400));
                }

                // 4. RETURN CLASSIFICATION RESULT FOR UI CONFIRMATION  
                var response = new FileUploadResponse
                {
                    Success = true,
                    FilePath = tempFilePath, // Temporary file path
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
        /// Processes a URL and automatically classifies its document type.
        /// </summary>
        /// <param name="request">The URL processing request.</param>
        /// <returns>A <see cref="UrlProcessResponse"/> containing the classification result for user confirmation.</returns>
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
        /// Cleans up temporary files older than the specified number of hours.
        /// </summary>
        /// <param name="hoursOld">The age in hours after which temporary files should be deleted (default: 24).</param>
        /// <returns>An <see cref="IActionResult"/> indicating the cleanup result.</returns>
        [HttpPost("cleanup-temp")]
        public IActionResult CleanupTempFiles(int hoursOld = 24)
        {
            try
            {
                var tempPath = Path.Combine(_uploadPath, "temp");
                if (!Directory.Exists(tempPath))
                {
                    return Ok(ApiResponse<object>.Success(null, "Temp folder does not exist"));
                }

                var cutoffTime = DateTime.Now.AddHours(-hoursOld);
                var tempFiles = Directory.GetFiles(tempPath);
                int deletedCount = 0;

                foreach (var file in tempFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffTime)
                    {
                        System.IO.File.Delete(file);
                        deletedCount++;
                    }
                }

                return Ok(ApiResponse<object>.Success(null, $"Cleaned up {deletedCount} temporary files"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Cleanup error: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Gets the Vietnamese name for a given document type.
        /// </summary>
        /// <param name="documentType">The document type enum value.</param>
        /// <returns>The Vietnamese string representation of the document type.</returns>
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