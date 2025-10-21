using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using VUniBox.Models;
using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;
using VUniBox.Services.Citation;
using VUniBox.Services.DocumentManagement;
using VUniBox.Services.Quota;

namespace VUniBox.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/document")]
    /// <summary>
    /// Controller for managing documents within the VUniBox application.
    /// Handles operations such as saving, moving to trash, restoring, permanent deletion, and retrieval of documents.
    /// </summary>
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentLifecycleService _documentLifecycleService;
        private readonly ICitationManagementService _citationManagementService;
        private readonly IQuotaManagementService _quotaManagementService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentController"/> class.
        /// </summary>
        /// <param name="documentLifecycleService">The document lifecycle service.</param>
        /// <param name="citationManagementService">The citation management service.</param>
        /// <param name="quotaManagementService">The quota management service.</param>
        public DocumentController(
            IDocumentLifecycleService documentLifecycleService,
            ICitationManagementService citationManagementService,
            IQuotaManagementService quotaManagementService)
        {
            _documentLifecycleService = documentLifecycleService;
            _citationManagementService = citationManagementService;
            _quotaManagementService = quotaManagementService;
        }

        /// <summary>
        /// Moves a document to the trash.
        /// </summary>
        /// <param name="request">The document trash request.</param>
        /// <returns>An <see cref="IActionResult"/> with the trash result.</returns>
        [HttpPost("trash")]
        public async Task<IActionResult> MoveToTrash([FromBody] DocumentTrashRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Yêu cầu không hợp lệ", 400));
                }

                var success = await _documentLifecycleService.MoveToTrashAsync(request.DocumentId, request.UserId);

                if (success)
                {
                    return Ok(ApiResponse<object>.Success(new { 
                        DocumentId = request.DocumentId,
                        ExpiryDate = DateTime.UtcNow.AddDays(10),
                        Message = "Tài liệu đã được chuyển vào thùng rác thành công"
                    }, "Tài liệu đã được chuyển vào thùng rác"));
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Fail("Không tìm thấy tài liệu hoặc tài liệu đã có trong thùng rác", 400));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Restores a document from the trash.
        /// </summary>
        /// <param name="request">The document trash request.</param>
        /// <returns>An <see cref="IActionResult"/> with the restore result.</returns>
        [HttpPost("restore")]
        public async Task<IActionResult> RestoreFromTrash([FromBody] DocumentTrashRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Yêu cầu không hợp lệ", 400));
                }

                var success = await _documentLifecycleService.RestoreFromTrashAsync(request.DocumentId, request.UserId);

                if (success)
                {
                    return Ok(ApiResponse<object>.Success(new { 
                        DocumentId = request.DocumentId,
                        Message = "Tài liệu đã được khôi phục từ thùng rác thành công"
                    }, "Tài liệu đã được khôi phục từ thùng rác"));
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Fail("Không tìm thấy tài liệu trong thùng rác", 400));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Retrieves documents by folder/category type with metadata and citations.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="folderType">The type of folder (Book, Word, Newspaper, PDF, Research, Others).</param>
        /// <param name="page">The page number (default: 1).</param>
        /// <param name="pageSize">The page size (default: 10).</param>
        /// <returns>An <see cref="IActionResult"/> with documents in the specified folder, including metadata and citations.</returns>
        [HttpGet("folder/{userId}/{folderType}")]
        public async Task<IActionResult> GetDocumentsByFolder(int userId, string folderType, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var (documents, totalCount) = await _documentLifecycleService.GetDocumentsByFolderWithCountAsync(userId, folderType, page, pageSize);

                // Create documents with citation data
                var documentsWithCitations = new List<DocumentWithCitationDto>();
                
                foreach (var document in documents)
                {
                    var docDto = new DocumentWithCitationDto(document);
                    
                    // Always get the latest citations from database to ensure fresh data
                    var latestDocument = await _documentLifecycleService.GetDocumentByIdAsync(document.DocumentId);
                    var existingCitations = latestDocument?.Citations?.ToList() ?? new List<Citations>();
                    
                    // If no citations exist, generate APA citation automatically
                    if (!existingCitations.Any())
                    {
                        try
                        {
                            var newCitation = await _citationManagementService.GenerateCitationAsync(document.DocumentId, "APA");
                            
                            if (newCitation != null)
                            {
                                // Create citation entity from response to add to the list
                                existingCitations = new List<Citations>
                                {
                                    new Citations
                                    {
                                        CitationId = 0, // Will be assigned by DB
                                        DocumentId = newCitation.DocumentId,
                                        UserId = document.UserId,
                                        Style = newCitation.Style,
                                        FormattedCitation = newCitation.FormattedCitation,
                                        InTextCitation = newCitation.InTextCitation,
                                        CreatedAt = DateTime.UtcNow
                                    }
                                };
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log citation generation error but continue
                            Console.WriteLine($"Failed to generate citation for document {document.DocumentId}: {ex.Message}");
                        }
                    }
                    
                    // Add citation to DTO (single citation only)
                    docDto.AddCitation(existingCitations);
                    documentsWithCitations.Add(docDto);
                }

                var response = new FolderDocumentsWithCitationResponse
                {
                    Success = true,
                    FolderType = folderType,
                    Documents = documentsWithCitations,
                    Message = $"Tài liệu trong thư mục {folderType} được truy xuất thành công với citation",
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize
                };

                return Ok(ApiResponse<FolderDocumentsWithCitationResponse>.Success(response, $"Thư mục {folderType} được truy xuất với citation"));
            }
            catch (Exception ex)
            {
                var errorResponse = new FolderDocumentsWithCitationResponse
                {
                    Success = false,
                    FolderType = folderType,
                    Message = $"Không thể truy xuất thư mục {folderType}",
                    Error = ex.Message
                };

                return StatusCode(500, ApiResponse<FolderDocumentsWithCitationResponse>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Retrieves a document by its ID, including citations and storage details.
        /// </summary>
        /// <param name="documentId">The ID of the document.</param>
        /// <returns>The document entity if found, otherwise null.</returns>
        [HttpGet("edit/{documentId}")]
        public async Task<IActionResult> GetDocumentForEdit(int documentId)
        {
            var doc = await _documentLifecycleService.GetDocumentByIdAsync(documentId);
            if (doc == null)
                return NotFound("Document not found.");

            var response = new
            {
                doc.DocumentId,
                doc.UserId,
                doc.Title,
                doc.Author,
                doc.Publisher,
                doc.Year,
                doc.Doi
            };

            return Ok(response);
        }


        /// <summary>
        /// Retrieves a summary of folders with document counts by type.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>An <see cref="IActionResult"/> with the folder summary and document counts.</returns>
        [HttpGet("folders/{userId}")]
        public async Task<IActionResult> GetFolderSummary(int userId)
        {
            try
            {
                var folderSummary = await _documentLifecycleService.GetFolderSummaryAsync(userId);

                var response = new FolderSummaryResponse
                {
                    Success = true,
                    FolderCounts = folderSummary,
                    Message = "Thống kê thư mục được truy xuất thành công",
                    TotalDocuments = folderSummary.Values.Sum()
                };

                return Ok(ApiResponse<FolderSummaryResponse>.Success(response, "Thống kê thư mục được truy xuất"));
            }
            catch (Exception ex)
            {
                var errorResponse = new FolderSummaryResponse
                {
                    Success = false,
                    Message = "Không thể truy xuất thống kê thư mục",
                    Error = ex.Message
                };

                return StatusCode(500, ApiResponse<FolderSummaryResponse>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Retrieves all saved documents for a user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>An <see cref="IActionResult"/> with the saved documents.</returns>
        [HttpGet("saved/{userId}")]
        public async Task<IActionResult> GetSavedDocuments(int userId)
        {
            try
            {
                // Lấy tất cả documents đã saved (status = Saved)
                var savedDocuments = await _documentLifecycleService.GetSavedDocumentsAsync(userId);

                // Create documents with citation data
                var documentsWithCitations = new List<DocumentWithCitationsDto>();
                
                foreach (var document in savedDocuments)
                {
                    // Get latest citation for the document
                    var latestDocument = await _documentLifecycleService.GetDocumentByIdAsync(document.DocumentId);
                    var existingCitation = latestDocument?.Citations?.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
                    
                    // If no citation exists, generate APA citation automatically (fast method)
                    if (existingCitation == null)
                    {
                        try
                        {
                            var (formattedCitation, inTextCitation) = await _citationManagementService.GenerateQuickCitationAsync(document, "APA");
                            
                            // Use the generated citation directly
                            var docWithCitation = new DocumentWithCitationsDto(document);
                            docWithCitation.CitationStyle = "APA";
                            docWithCitation.FormattedCitation = formattedCitation;
                            docWithCitation.InTextCitation = inTextCitation;
                            documentsWithCitations.Add(docWithCitation);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Failed to generate quick citation for document {document.DocumentId}: {ex.Message}");
                            documentsWithCitations.Add(new DocumentWithCitationsDto(document));
                        }
                    }
                    else
                    {
                        documentsWithCitations.Add(new DocumentWithCitationsDto(document, existingCitation));
                    }
                }

                var response = new SavedDocumentsResponse
                {
                    Success = true,
                    SavedDocuments = documentsWithCitations.Cast<DocumentDto>().ToList(),
                    Message = "Tài liệu đã lưu được truy xuất thành công",
                    TotalCount = documentsWithCitations.Count
                };

                return Ok(ApiResponse<SavedDocumentsResponse>.Success(response, "Tài liệu đã lưu được truy xuất"));
            }
            catch (Exception ex)
            {
                var errorResponse = new SavedDocumentsResponse
                {
                    Success = false,
                    Message = "Không thể truy xuất tài liệu đã lưu",
                    Error = ex.Message
                };

                return StatusCode(500, ApiResponse<SavedDocumentsResponse>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Retrieves all documents (saved and trash) for a user with optional filtering by status and type.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="status">Optional: The document status to filter by.</param>
        /// <param name="type">Optional: The document type to filter by.</param>
        /// <returns>An <see cref="IActionResult"/> with the filtered documents.</returns>
        [HttpGet("all/{userId}")]
        public async Task<IActionResult> GetAllDocuments(int userId, [FromQuery] string? status = null, [FromQuery] string? type = null)
        {
            try
            {
                var allDocuments = await _documentLifecycleService.GetAllDocumentsAsync(userId, status, type);

                // Create documents with citation data
                var documentsWithCitations = new List<DocumentWithCitationsDto>();
                
                foreach (var document in allDocuments)
                {
                    // Get latest citation for the document
                    var latestDocument = await _documentLifecycleService.GetDocumentByIdAsync(document.DocumentId);
                    var existingCitation = latestDocument?.Citations?.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
                    
                    // If no citation exists, generate APA citation automatically (fast method)
                    if (existingCitation == null)
                    {
                        try
                        {
                            var (formattedCitation, inTextCitation) = await _citationManagementService.GenerateQuickCitationAsync(document, "APA");
                            
                            // Use the generated citation directly
                            var docWithCitation = new DocumentWithCitationsDto(document);
                            docWithCitation.CitationStyle = "APA";
                            docWithCitation.FormattedCitation = formattedCitation;
                            docWithCitation.InTextCitation = inTextCitation;
                            documentsWithCitations.Add(docWithCitation);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Failed to generate quick citation for document {document.DocumentId}: {ex.Message}");
                            documentsWithCitations.Add(new DocumentWithCitationsDto(document));
                        }
                    }
                    else
                    {
                        documentsWithCitations.Add(new DocumentWithCitationsDto(document, existingCitation));
                    }
                }

                var response = new AllDocumentsResponse
                {
                    Success = true,
                    Documents = documentsWithCitations.Cast<DocumentDto>().ToList(),
                    Message = "Tất cả tài liệu được truy xuất thành công",
                    TotalCount = documentsWithCitations.Count,
                    FilteredBy = new { Status = status, Type = type }
                };

                return Ok(ApiResponse<AllDocumentsResponse>.Success(response, "Tất cả tài liệu được truy xuất"));
            }
            catch (Exception ex)
            {
                var errorResponse = new AllDocumentsResponse
                {
                    Success = false,
                    Message = "Không thể truy xuất tài liệu",
                    Error = ex.Message
                };

                return StatusCode(500, ApiResponse<AllDocumentsResponse>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Retrieves all documents currently in the trash for a user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>An <see cref="IActionResult"/> with the trash documents.</returns>
        [HttpGet("trash/{userId}")]
        public async Task<IActionResult> GetTrashDocuments(int userId)
        {
            try
            {
                var trashDocuments = await _documentLifecycleService.GetTrashDocumentsAsync(userId);

                var response = new TrashResponse
                {
                    Success = true,
                    TrashDocuments = trashDocuments.Select(d => new DocumentDto(d)).ToList(),
                    Message = "Tài liệu trong thùng rác đã được truy xuất thành công",
                    TotalCount = trashDocuments.Count
                };

                return Ok(ApiResponse<TrashResponse>.Success(response, "Tài liệu trong thùng rác đã được truy xuất"));
            }
            catch (Exception ex)
            {
                var errorResponse = new TrashResponse
                {
                    Success = false,
                    Message = "Không thể truy xuất tài liệu trong thùng rác",
                    Error = ex.Message
                };

                return StatusCode(500, ApiResponse<TrashResponse>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Deletes a document permanently.
        /// </summary>
        /// <param name="request">The document trash request.</param>
        /// <returns>An <see cref="IActionResult"/> with the delete result.</returns>
        [HttpDelete("permanent")]
        public async Task<IActionResult> DeletePermanently([FromBody] DocumentTrashRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.Fail("Yêu cầu không hợp lệ", 400));
                }

                var success = await _documentLifecycleService.DeletePermanentlyAsync(request.DocumentId, request.UserId);

                if (success)
                {
                    return Ok(ApiResponse<object>.Success(new
                    {
                        DocumentId = request.DocumentId,
                        Message = "Tài liệu đã được xóa vĩnh viễn"
                    }, "Tài liệu đã được xóa vĩnh viễn"));
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Fail("Không tìm thấy tài liệu", 400));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Updates document metadata fields such as Year, Author, Publisher, and Title.
        /// </summary>
        /// <param name="request">The update request.</param>
        /// <returns>An <see cref="IActionResult"/> indicating success or failure.</returns>
        [HttpPut("update")]
        public async Task<IActionResult> UpdateDocument([FromBody] DocumentUpdateRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest(ApiResponse<object>.Fail("Yêu cầu không hợp lệ", 400));

                var success = await _documentLifecycleService.UpdateDocumentInfoAsync(request);

                if (success)
                {
                    return Ok(ApiResponse<object>.Success(new
                    {
                        request.DocumentId,
                        Message = "Tài liệu đã được cập nhật thành công"
                    }, "Cập nhật tài liệu thành công"));
                }
                else
                {
                    return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài liệu hoặc bạn không có quyền cập nhật", 404));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

    }
}








