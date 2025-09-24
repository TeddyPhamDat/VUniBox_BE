using Microsoft.AspNetCore.Mvc;
using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.DTO;
using VUniBox.Services.Metadata;
using VUniBox.Services.DocumentManagement;
using VUniBox.Models.Enum;
using System.Text.Json;

namespace VUniBox.Controllers
{
    /// <summary>
    /// Controller for document metadata extraction and processing
    /// Handles metadata extraction, document saving, and citation generation
    /// </summary>
    [ApiController]
    [Route("api/documentMetadata")]
    public class DocumentMetadataController : ControllerBase
    {
        private readonly IMetadataExtractionService _metadataExtractionService;
        private readonly IDocumentLifecycleService _documentLifecycleService;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentMetadataController"/> class.
        /// </summary>
        /// <param name="metadataExtractionService">The metadata extraction service.</param>
        /// <param name="documentLifecycleService">The document lifecycle service.</param>
        /// <param name="configuration">The application configuration.</param>
        public DocumentMetadataController(
            IMetadataExtractionService metadataExtractionService,
            IDocumentLifecycleService documentLifecycleService,
            IConfiguration configuration)
        {
            _metadataExtractionService = metadataExtractionService;
            _documentLifecycleService = documentLifecycleService;
            _configuration = configuration;
        }

        /// <summary>
        /// Extracts metadata and saves the document based on user confirmation.
        /// This is the final step after classification confirmation.
        /// </summary>
        /// <param name="request">The user confirmation request with document details.</param>
        /// <returns>An <see cref="IActionResult"/> with the complete metadata extraction and document save result.</returns>
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
                string? finalFilePath = null;
                
                if (!string.IsNullOrEmpty(request.FilePath))
                {
                    // File processing - extract metadata from uploaded file
                    metadata = await _metadataExtractionService.ExtractFromFileAsync(
                        request.FilePath, 
                        request.FileName ?? Path.GetFileName(request.FilePath), 
                        request.DocumentType);

                    // 1.1. AUTO-FIX CORRUPTED TEXT using AI
                    metadata = await FixCorruptedTextInMetadata(metadata);

                    // 1.2. If metadata is mostly empty, try to generate from filename
                    if (IsMetadataEmpty(metadata) && !string.IsNullOrEmpty(request.FileName))
                    {
                        metadata = await GenerateMetadataFromFilename(metadata, request.FileName, request.DocumentType);
                    }

                    // Handle file movement from temp to permanent location
                    if (request.SaveToFolder)
                    {
                        // Move file from temp to permanent uploads folder
                        var uploadsPath = _configuration["FileUpload:Path"] ?? "uploads";
                        if (!Directory.Exists(uploadsPath))
                        {
                            Directory.CreateDirectory(uploadsPath);
                        }

                        var fileName = Path.GetFileName(request.FilePath);
                        finalFilePath = Path.Combine(uploadsPath, fileName);

                        // Move file from temp to uploads
                        if (System.IO.File.Exists(request.FilePath))
                        {
                            System.IO.File.Move(request.FilePath, finalFilePath);
                        }
                    }
                    else
                    {
                        // User chose not to save - delete temp file
                        if (System.IO.File.Exists(request.FilePath))
                        {
                            System.IO.File.Delete(request.FilePath);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(request.Url))
                {
                    try
                    {
                        Console.WriteLine($"[DEBUG] Starting URL metadata extraction for: {request.Url}");
                        
                        // URL processing - extract metadata from web page
                        metadata = await _metadataExtractionService.ExtractFromUrlAsync(
                            request.Url, 
                            request.DocumentType);
                            
                        Console.WriteLine($"[DEBUG] URL extraction completed. Title: {metadata.Title}");
                            
                        // 1.1. AUTO-FIX CORRUPTED TEXT using AI
                        metadata = await FixCorruptedTextInMetadata(metadata);

                        // 1.2. If metadata is mostly empty, try to generate from URL
                        if (IsMetadataEmpty(metadata) && !string.IsNullOrEmpty(request.Url))
                        {
                            Console.WriteLine($"[DEBUG] Metadata is empty, generating from URL");
                            metadata = await GenerateMetadataFromUrl(metadata, request.Url, request.DocumentType);
                        }
                    }
                    catch (Exception urlEx)
                    {
                        Console.WriteLine($"[ERROR] URL extraction failed for {request.Url}: {urlEx.Message}");
                        
                        // Create fallback metadata if URL extraction completely fails
                        metadata = new DocumentMetadataDto
                        {
                            URL = request.Url,
                            Title = ExtractTitleFromUrl(request.Url),
                            Description = $"Không thể trích xuất metadata từ URL. Lỗi: {urlEx.Message}",
                            Source = GetDomainFromUrl(request.Url),
                            Language = "vi",
                            RetrievedDate = DateTime.UtcNow
                        };
                        
                        Console.WriteLine($"[DEBUG] Created fallback metadata with title: {metadata.Title}");
                    }
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
                    finalFilePath ?? request.Url); // Use final file path or URL

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
        /// Automatically fixes corrupted text in metadata using AI.
        /// </summary>
        /// <param name="metadata">The metadata object with potentially corrupted text.</param>
        /// <returns>The metadata object with fixed text.</returns>
        private async Task<DocumentMetadataDto> FixCorruptedTextInMetadata(DocumentMetadataDto metadata)
        {
            try
            {
                // Check and fix corrupted text in common fields
                if (!string.IsNullOrEmpty(metadata.Title) && IsCorruptedText(metadata.Title))
                {
                    Console.WriteLine($"[DEBUG] Fixing corrupted title: {metadata.Title}");
                    metadata.Title = await FixTextWithAI(metadata.Title, "title");
                    Console.WriteLine($"[DEBUG] Fixed title: {metadata.Title}");
                }

                if (!string.IsNullOrEmpty(metadata.Abstract) && IsCorruptedText(metadata.Abstract))
                {
                    Console.WriteLine($"[DEBUG] Fixing corrupted abstract: {metadata.Abstract}");
                    metadata.Abstract = await FixTextWithAI(metadata.Abstract, "abstract");
                    Console.WriteLine($"[DEBUG] Fixed abstract: {metadata.Abstract}");
                }

                if (!string.IsNullOrEmpty(metadata.Author) && IsCorruptedText(metadata.Author))
                {
                    metadata.Author = await FixTextWithAI(metadata.Author, "author");
                }

                if (!string.IsNullOrEmpty(metadata.Publisher) && IsCorruptedText(metadata.Publisher))
                {
                    metadata.Publisher = await FixTextWithAI(metadata.Publisher, "publisher");
                }

                if (!string.IsNullOrEmpty(metadata.Journal) && IsCorruptedText(metadata.Journal))
                {
                    metadata.Journal = await FixTextWithAI(metadata.Journal, "journal");
                }

                if (!string.IsNullOrEmpty(metadata.Description) && IsCorruptedText(metadata.Description))
                {
                    metadata.Description = await FixTextWithAI(metadata.Description, "description");
                }

                if (!string.IsNullOrEmpty(metadata.Keywords) && IsCorruptedText(metadata.Keywords))
                {
                    metadata.Keywords = await FixTextWithAI(metadata.Keywords, "keywords");
                }

                if (!string.IsNullOrEmpty(metadata.Subject) && IsCorruptedText(metadata.Subject))
                {
                    metadata.Subject = await FixTextWithAI(metadata.Subject, "subject");
                }

                return metadata;
            }
            catch (Exception)
            {
                // If fixing fails, return original metadata
                return metadata;
            }
        }

        /// <summary>
        /// Checks if metadata is mostly empty (needs AI generation).
        /// </summary>
        /// <param name="metadata">The metadata to check.</param>
        /// <returns>True if metadata is mostly empty.</returns>
        private bool IsMetadataEmpty(DocumentMetadataDto metadata)
        {
            return string.IsNullOrWhiteSpace(metadata.Title) &&
                   string.IsNullOrWhiteSpace(metadata.Abstract) &&
                   string.IsNullOrWhiteSpace(metadata.Author) &&
                   string.IsNullOrWhiteSpace(metadata.Publisher) &&
                   string.IsNullOrWhiteSpace(metadata.Subject);
        }

        /// <summary>
        /// Generates metadata from filename using AI when extraction fails.
        /// </summary>
        /// <param name="metadata">The existing metadata object.</param>
        /// <param name="filename">The filename to analyze.</param>
        /// <param name="documentType">The document type.</param>
        /// <returns>Metadata with AI-generated content.</returns>
        private async Task<DocumentMetadataDto> GenerateMetadataFromFilename(DocumentMetadataDto metadata, string filename, DocumentType documentType)
        {
            try
            {
                using var httpClient = new HttpClient();
                var apiKey = _configuration["GoogleAI:ApiKey"];
                
                if (string.IsNullOrEmpty(apiKey))
                {
                    return metadata; // Return original if no API key
                }

                var prompt = $@"Bạn là chuyên gia phân tích tài liệu học thuật Việt Nam. 
Dựa vào tên file sau, hãy tạo metadata phù hợp:

Tên file: ""{filename}""
Loại tài liệu: {GetDocumentTypeName(documentType)}

Hãy trả về thông tin theo format JSON sau (chỉ trả JSON, không giải thích):
{{
  ""title"": ""Tiêu đề tài liệu tiếng Việt phù hợp"",
  ""author"": ""Tác giả (nếu có thể suy đoán)"",
  ""abstract"": ""Tóm tắt nội dung 100-150 từ dựa trên tên file"",
  ""subject"": ""Chủ đề chính"",
  ""keywords"": ""từ khóa, liên quan, đến, tài liệu"",
  ""language"": ""vi""
}}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.3,
                        maxOutputTokens = 2048
                    }
                };

                var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={apiKey}",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                    
                    var aiResponse = jsonDoc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    // Parse AI response JSON
                    if (!string.IsNullOrEmpty(aiResponse))
                    {
                        try
                        {
                            // Clean AI response (remove markdown code blocks if any)
                            var cleanJson = aiResponse.Trim();
                            if (cleanJson.StartsWith("```json"))
                            {
                                cleanJson = cleanJson.Substring(7);
                            }
                            if (cleanJson.EndsWith("```"))
                            {
                                cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
                            }

                            var aiMetadata = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(cleanJson);
                            
                            if (aiMetadata.TryGetProperty("title", out var title))
                                metadata.Title = title.GetString() ?? metadata.Title;
                            if (aiMetadata.TryGetProperty("author", out var author))
                                metadata.Author = author.GetString() ?? metadata.Author;
                            if (aiMetadata.TryGetProperty("abstract", out var abstractText))
                                metadata.Abstract = abstractText.GetString() ?? metadata.Abstract;
                            if (aiMetadata.TryGetProperty("subject", out var subject))
                                metadata.Subject = subject.GetString() ?? metadata.Subject;
                            if (aiMetadata.TryGetProperty("keywords", out var keywords))
                                metadata.Keywords = keywords.GetString() ?? metadata.Keywords;
                            if (aiMetadata.TryGetProperty("language", out var language))
                                metadata.Language = language.GetString() ?? metadata.Language;
                        }
                        catch
                        {
                            // If JSON parsing fails, fallback to simple title generation
                            metadata.Title = GenerateTitleFromFilename(filename);
                        }
                    }
                }

                return metadata;
            }
            catch (Exception)
            {
                // If AI fails, at least set a basic title
                metadata.Title = GenerateTitleFromFilename(filename);
                return metadata;
            }
        }

        /// <summary>
        /// Generates metadata from URL using AI when extraction fails.
        /// </summary>
        /// <param name="metadata">The existing metadata object.</param>
        /// <param name="url">The URL to analyze.</param>
        /// <param name="documentType">The document type.</param>
        /// <returns>Metadata with AI-generated content.</returns>
        private async Task<DocumentMetadataDto> GenerateMetadataFromUrl(DocumentMetadataDto metadata, string url, DocumentType documentType)
        {
            try
            {
                // Similar implementation to GenerateMetadataFromFilename but for URLs
                metadata.Title = metadata.Title ?? new Uri(url).Host;
                metadata.URL = url;
                metadata.Source = new Uri(url).Host;
                return metadata;
            }
            catch (Exception)
            {
                return metadata;
            }
        }

        /// <summary>
        /// Generates a basic title from filename.
        /// </summary>
        /// <param name="filename">The filename to process.</param>
        /// <returns>A formatted title.</returns>
        private string GenerateTitleFromFilename(string filename)
        {
            try
            {
                // Remove extension and GUID prefix
                var title = Path.GetFileNameWithoutExtension(filename);
                
                // Remove GUID pattern (36 chars + underscore)
                if (title.Length > 37 && title[36] == '_')
                {
                    title = title.Substring(37);
                }
                
                // Replace dashes and underscores with spaces
                title = title.Replace("-", " ").Replace("_", " ");
                
                // Capitalize first letter of each word
                title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(title.ToLower());
                
                return title;
            }
            catch
            {
                return filename;
            }
        }

        /// <summary>
        /// Checks if text appears to be corrupted (contains encoding issues).
        /// </summary>
        /// <param name="text">The text to check.</param>
        /// <returns>True if text appears corrupted, false otherwise.</returns>
        private bool IsCorruptedText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            Console.WriteLine($"[DEBUG] Checking corruption for text: {text}");

            // Count corrupted characters
            int corruptedCharCount = 0;
            int totalChars = text.Length;

            // Common signs of corrupted Vietnamese text
            if (text.Contains("�")) 
            {
                int replacementChars = text.Count(c => c == '�');
                corruptedCharCount += replacementChars * 2; // High penalty for replacement chars
                Console.WriteLine($"[DEBUG] Found {replacementChars} replacement characters");
            }
            
            if (text.Contains("?") && text.Length > 20) 
            {
                int questionMarks = text.Count(c => c == '?');
                corruptedCharCount += questionMarks; // Question marks in long text
                Console.WriteLine($"[DEBUG] Found {questionMarks} question marks in long text");
            }
            
            // Specific Vietnamese corruption patterns
            string[] corruptionPatterns = {
                "GI�O", "TU?NG", "H?", "CH�", "D?I", "PHUONG", "PH�P", "NGHI�N",
                "V�", "� NGHIA", "M�N", "D?NG", "C?NG", "VI?T", "QU?C", "TH?",
                "oAR", "cP", "SD", "OM", "to�n", "l?n", "n�u", "kh�i", "bi?u",
                "l OM", "OM oAR", "oAR cP", "cP SD" // Additional patterns from your example
            };

            foreach (var pattern in corruptionPatterns)
            {
                if (text.Contains(pattern)) 
                {
                    corruptedCharCount += pattern.Length;
                    Console.WriteLine($"[DEBUG] Found corruption pattern: {pattern}");
                }
            }

            // If more than 10% of text appears corrupted, consider it corrupted (lowered threshold)
            double corruptionRatio = (double)corruptedCharCount / totalChars;
            bool isCorrupted = corruptionRatio > 0.10;
            
            Console.WriteLine($"[DEBUG] Corruption ratio: {corruptionRatio:P2}, Is corrupted: {isCorrupted}");
            
            return isCorrupted;
        }

        /// <summary>
        /// Uses Gemini AI to fix corrupted Vietnamese text encoding or generate new content.
        /// </summary>
        /// <param name="corruptedText">The corrupted text to fix.</param>
        /// <param name="fieldType">The type of field (abstract, title, etc.) for better context.</param>
        /// <returns>The fixed or regenerated text.</returns>
        private async Task<string> FixTextWithAI(string corruptedText, string fieldType = "text")
        {
            try
            {
                Console.WriteLine($"[DEBUG] FixTextWithAI called for {fieldType}: {corruptedText}");
                
                using var httpClient = new HttpClient();
                var apiKey = _configuration["GoogleAI:ApiKey"];
                
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("[DEBUG] No API key found, returning original text");
                    return corruptedText; // Return original if no API key
                }

                Console.WriteLine("[DEBUG] API key found, proceeding with AI fix");

                string prompt;

                // For abstract field, if text is heavily corrupted, try to regenerate
                if (fieldType.ToLower() == "abstract" && IsHeavilyCorrupted(corruptedText))
                {
                    prompt = $@"Bạn là một chuyên gia xử lý văn bản tiếng Việt. 
Văn bản sau đây bị lỗi encoding nghiêm trọng từ một tài liệu học thuật Việt Nam:

""{corruptedText}""

Nhiệm vụ của bạn:
1. Nếu có thể đọc hiểu được ý nghĩa, hãy viết lại thành một abstract/tóm tắt tiếng Việt chuẩn, rõ ràng
2. Nếu không thể hiểu được, hãy tạo một abstract phù hợp dựa trên những từ khóa có thể nhận biết được

Dựa vào các từ như ""GIÁO TRÌNH"", ""TƯ TƯỞNG HỒ CHÍ MINH"", ""NGHIÊN CỨU"", hãy tạo abstract phù hợp.

Abstract tiếng Việt chuẩn (khoảng 100-200 từ):";
                }
                else
                {
                    prompt = $@"Bạn là một chuyên gia sửa lỗi encoding văn bản tiếng Việt. 
Văn bản sau bị lỗi encoding:

""{corruptedText}""

Hãy sửa lại thành tiếng Việt đúng, giữ nguyên ý nghĩa và cấu trúc. 
Chỉ trả về text đã được sửa, không cần giải thích.

Ví dụ sửa lỗi:
- ""GI�O TR�NH"" -> ""GIÁO TRÌNH""
- ""TU?NG H? CH� MINH"" -> ""TƯ TƯỞNG HỒ CHÍ MINH""
- ""D?I TU?NG"" -> ""ĐỐI TƯỢNG""
- ""PHUONG PH�P NGHI�N C?U"" -> ""PHƯƠNG PHÁP NGHIÊN CỨU""
- ""� NGHIA H?C T?P"" -> ""Ý NGHĨA HỌC TẬP""
- ""D?ng C?ng s?n Vi?t Nam"" -> ""Đảng Cộng sản Việt Nam""
- ""to�n qu?c"" -> ""toàn quốc""

Text đã sửa:";
                }

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = fieldType.ToLower() == "abstract" && IsHeavilyCorrupted(corruptedText) ? 0.3 : 0.1,
                        maxOutputTokens = fieldType.ToLower() == "abstract" ? 4096 : 2048
                    }
                };

                var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={apiKey}",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG] AI response received: {responseContent}");
                    
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                    
                    var fixedText = jsonDoc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    Console.WriteLine($"[DEBUG] Extracted fixed text: {fixedText}");
                    return fixedText?.Trim() ?? corruptedText;
                }
                else
                {
                    Console.WriteLine($"[DEBUG] AI request failed: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return corruptedText; // Return original if API fails
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Exception in FixTextWithAI: {ex.Message}");
                return corruptedText;
            }
        }

        /// <summary>
        /// Checks if text is heavily corrupted (needs regeneration rather than just fixing).
        /// </summary>
        /// <param name="text">The text to check.</param>
        /// <returns>True if text is heavily corrupted.</returns>
        private bool IsHeavilyCorrupted(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            int corruptedCharCount = 0;
            int totalChars = text.Length;

            // Count replacement characters and question marks
            corruptedCharCount += text.Count(c => c == '�') * 3; // High penalty
            corruptedCharCount += text.Count(c => c == '?') * 1; // Medium penalty
            
            // Count numbers and random characters that shouldn't be in Vietnamese text
            corruptedCharCount += text.Count(c => char.IsDigit(c) && !"0123456789".Contains(c));
            
            // Specific corruption patterns
            string[] heavyCorruptionPatterns = { "OM oAR cP SD", "l OM", "oAR", "cP SD" };
            foreach (var pattern in heavyCorruptionPatterns)
            {
                if (text.Contains(pattern)) corruptedCharCount += pattern.Length * 2;
            }

            // If more than 30% appears heavily corrupted, regenerate
            return (double)corruptedCharCount / totalChars > 0.3;
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

        /// <summary>
        /// Extracts a basic title from URL when metadata extraction fails.
        /// </summary>
        /// <param name="url">The URL to extract title from.</param>
        /// <returns>A basic title based on the URL.</returns>
        private string ExtractTitleFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var domain = uri.Host.Replace("www.", "");
                
                // Try to get meaningful title from path
                var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathSegments.Length > 0)
                {
                    var lastSegment = pathSegments.Last();
                    // Remove file extensions and clean up
                    var title = Path.GetFileNameWithoutExtension(lastSegment)
                        .Replace("-", " ")
                        .Replace("_", " ")
                        .Replace("%20", " ");
                    
                    if (!string.IsNullOrEmpty(title) && title.Length > 3)
                    {
                        return $"{title} - {domain}";
                    }
                }
                
                return $"Tài liệu từ {domain}";
            }
            catch
            {
                return "Tài liệu từ website";
            }
        }

        /// <summary>
        /// Gets the domain name from a URL.
        /// </summary>
        /// <param name="url">The URL to extract domain from.</param>
        /// <returns>The domain name.</returns>
        private string GetDomainFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host.Replace("www.", "");
            }
            catch
            {
                return "Unknown";
            }
        }
    }

    /// <summary>
    /// Represents a request for metadata extraction only.
    /// </summary>
    public class MetadataExtractionRequest
    {
        /// <summary>
        /// Gets or sets the file path for file-based metadata extraction.
        /// </summary>
        public string? FilePath { get; set; }
        /// <summary>
        /// Gets or sets the file name for file-based metadata extraction.
        /// </summary>
        public string? FileName { get; set; }
        /// <summary>
        /// Gets or sets the URL for URL-based metadata extraction.
        /// </summary>
        public string? Url { get; set; }
        /// <summary>
        /// Gets or sets the document type for classification context.
        /// </summary>
        public DocumentType DocumentType { get; set; }
    }
}