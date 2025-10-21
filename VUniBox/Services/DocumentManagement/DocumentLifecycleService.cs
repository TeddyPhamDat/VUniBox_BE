using Microsoft.EntityFrameworkCore;
using VUniBox.DBContext;
using VUniBox.Models;
using VUniBox.Models.DTO;
using VUniBox.Models.DTO.Request;
using VUniBox.Models.Enum;
using VUniBox.Services.Usage;

namespace VUniBox.Services.DocumentManagement
{
    /// <summary>
    /// Service for managing the lifecycle of documents, including saving, moving to trash, restoring, and permanent deletion.
    /// </summary>
    public class DocumentLifecycleService : IDocumentLifecycleService
    {
        private readonly VUniBoxContext _context;
        private readonly IUsageTrackingService? _usageTrackingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentLifecycleService"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="usageTrackingService">The usage tracking service.</param>
        public DocumentLifecycleService(VUniBoxContext context, IUsageTrackingService? usageTrackingService = null)
        {
            _context = context;
            _usageTrackingService = usageTrackingService;
        }

        /// <summary>
        /// Retrieves a document by its ID.
        /// </summary>
        /// <param name="documentId">The ID of the document.</param>
        /// <returns>A <see cref="Documents"/> object if found, otherwise null.</returns>
        public async Task<Documents?> GetDocumentByIdAsync(int documentId)
        {
            return await _context.Documents
                .Include(d => d.Citations)
                .Include(d => d.DocumentStorage)
                .FirstOrDefaultAsync(d => d.DocumentId == documentId);
        }

        /// <summary>
        /// Saves a new document or updates an existing one.
        /// </summary>
        /// <param name="userId">The ID of the user owning the document.</param>
        /// <param name="metadata">The metadata of the document.</param>
        /// <param name="documentType">The type of the document.</param>
        /// <param name="filePath">Optional: The file path of the document.</param>
        /// <returns>The saved <see cref="Documents"/> object.</returns>
        public async Task<Documents> SaveDocumentAsync(int userId, DocumentMetadataDto metadata, DocumentType documentType, string? filePath = null)
        {
            try
            {
                Console.WriteLine($"[DocumentLifecycleService] Saving document with metadata:");
                Console.WriteLine($"  - Title: '{metadata.Title}'");
                Console.WriteLine($"  - Author: '{metadata.Author}'");
                Console.WriteLine($"  - Authors: '{metadata.Authors}'");
                Console.WriteLine($"  - Publisher: '{metadata.Publisher}'");
                
                // Tạo document record
                var document = new Documents
                {
                    UserId = userId,
                    Title = metadata.Title,
                    FilePath = filePath ?? metadata.FilePath,
                    SourceUrl = metadata.URL,
                    DocumentType = (int)documentType,
                    Status = DocumentStatus.Saved.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    TrashDate = null, // Không có expiry date cho saved documents
                    
                    // Metadata fields
                    Author = metadata.Author,
                    Authors = metadata.Authors,
                    PublicationDate = metadata.PublicationDate,
                    Publisher = metadata.Publisher,
                    Journal = metadata.Journal,
                    Volume = metadata.Volume,
                    Issue = metadata.Issue,
                    Pages = metadata.Pages,
                    Doi = metadata.DOI,
                    Isbn = metadata.ISBN,
                    Abstract = metadata.Abstract,
                    Description = metadata.Description,
                    Keywords = metadata.Keywords,
                    Subject = metadata.Subject,
                    Source = metadata.Source,
                    Language = metadata.Language,
                    RetrievedDate = metadata.RetrievedDate
                };

                _context.Documents.Add(document);
                await _context.SaveChangesAsync();

                // Tạo document storage record nếu có file
                if (!string.IsNullOrEmpty(filePath) || !string.IsNullOrEmpty(metadata.FilePath))
                {
                    var documentStorage = new DocumentStorage
                    {
                        DocumentId = document.DocumentId,
                        UserId = userId,
                        FileName = Path.GetFileName(filePath ?? metadata.FilePath ?? ""),
                        FilePath = filePath ?? metadata.FilePath,
                        Title = metadata.Title,
                        AuthorName = metadata.Author,
                        Year = metadata.PublicationDate?.Year,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        FileSize = metadata.FileSize,
                        FileType = metadata.FileType,
                        FavoriteFlag = false,
                        
                        // Additional metadata
                        Authors = metadata.Authors,
                        PublicationDate = metadata.PublicationDate,
                        Publisher = metadata.Publisher,
                        Journal = metadata.Journal,
                        Volume = metadata.Volume,
                        Issue = metadata.Issue,
                        Pages = metadata.Pages,
                        Doi = metadata.DOI,
                        Isbn = metadata.ISBN,
                        Abstract = metadata.Abstract,
                        Description = metadata.Description,
                        Keywords = metadata.Keywords,
                        Subject = metadata.Subject,
                        Source = metadata.Source,
                        Language = metadata.Language,
                        RetrievedDate = metadata.RetrievedDate
                    };

                    _context.DocumentStorage.Add(documentStorage);
                    await _context.SaveChangesAsync();
                }

                // Return document without navigation properties to avoid cycles
                // Detach from context to prevent navigation properties from being loaded
                _context.Entry(document).State = EntityState.Detached;

                // Update storage usage after saving document
                if (_usageTrackingService != null)
                {
                    await _usageTrackingService.UpdateStorageUsageAsync(userId);
                }
                
                return document;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving document: {ex.Message}");
            }
        }

        /// <summary>
        /// Moves a document to the trash.
        /// </summary>
        /// <param name="documentId">The ID of the document to move.</param>
        /// <param name="userId">The ID of the user owning the document.</param>
        /// <returns>True if the document was successfully moved to trash, false otherwise.</returns>
        public async Task<bool> MoveToTrashAsync(int documentId, int userId)
        {
            try
            {
                var document = await _context.Documents
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId && d.UserId == userId);

                if (document == null)
                    return false;

                // Cập nhật status và set expiry date (10 ngày từ bây giờ)
                document.Status = DocumentStatus.Trash.ToString();
                document.TrashDate = DateTime.UtcNow.AddDays(10);

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error moving document to trash: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores a document from the trash.
        /// </summary>
        /// <param name="documentId">The ID of the document to restore.</param>
        /// <param name="userId">The ID of the user owning the document.</param>
        /// <returns>True if the document was successfully restored, false otherwise.</returns>
        public async Task<bool> RestoreFromTrashAsync(int documentId, int userId)
        {
            try
            {
                var document = await _context.Documents
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId && d.UserId == userId);

                if (document == null || document.Status != DocumentStatus.Trash.ToString())
                    return false;

                // Restore document
                document.Status = DocumentStatus.Saved.ToString();
                document.TrashDate = null;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error restoring document from trash: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a document permanently.
        /// </summary>
        /// <param name="documentId">The ID of the document to delete.</param>
        /// <param name="userId">The ID of the user owning the document.</param>
        /// <returns>True if the document was successfully deleted, false otherwise.</returns>
        public async Task<bool> DeletePermanentlyAsync(int documentId, int userId)
        {
            try
            {
                var document = await _context.Documents
                    .Include(d => d.DocumentStorage)
                    .Include(d => d.Citations)
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId && d.UserId == userId);

                if (document == null)
                    return false;

                // Xóa tất cả related records
                _context.DocumentStorage.RemoveRange(document.DocumentStorage);
                _context.Citations.RemoveRange(document.Citations);
                _context.Documents.Remove(document);

                await _context.SaveChangesAsync();

                // Update storage usage after deleting document
                if (_usageTrackingService != null)
                {
                    await _usageTrackingService.UpdateStorageUsageAsync(userId);
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting document permanently: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a list of documents in the trash for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A list of <see cref="Documents"/> in the trash.</returns>
        public async Task<List<Documents>> GetTrashDocumentsAsync(int userId)
        {
            try
            {
                return await _context.Documents
                    .Where(d => d.UserId == userId && d.Status == DocumentStatus.Trash.ToString())
                    .OrderByDescending(d => d.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting trash documents: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a list of saved documents for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A list of saved <see cref="Documents"/>.</returns>
        public async Task<List<Documents>> GetSavedDocumentsAsync(int userId)
        {
            try
            {
                return await _context.Documents
                    .Where(d => d.UserId == userId && d.Status == DocumentStatus.Saved.ToString())
                    .OrderByDescending(d => d.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting saved documents: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves all documents (saved and trash) for a user with optional filtering.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="status">Optional: The document status to filter by.</param>
        /// <param name="type">Optional: The document type to filter by.</param>
        /// <returns>A list of filtered <see cref="Documents"/>.</returns>
        public async Task<List<Documents>> GetAllDocumentsAsync(int userId, string? status = null, string? type = null)
        {
            try
            {
                var query = _context.Documents.Where(d => d.UserId == userId);

                // Filter by status if provided
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(d => d.Status == status);
                }

                // Filter by type if provided
                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(d => d.DocumentType.ToString() == type);
                }

                return await query
                    .OrderByDescending(d => d.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting all documents: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a paginated list of documents by folder type.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="folderType">The type of folder (e.g., "Book", "Word").</param>
        /// <param name="page">The page number.</param>
        /// <param name="pageSize">The number of documents per page.</param>
        /// <returns>A paginated list of <see cref="Documents"/> in the specified folder.</returns>
        public async Task<List<Documents>> GetDocumentsByFolderAsync(int userId, string folderType, int page = 1, int pageSize = 10)
        {
            try
            {
                // Map folder type to document type enum
                var documentType = folderType.ToLower() switch
                {
                    "book" => DocumentType.Book,
                    "word" => DocumentType.Word, 
                    "newspaper" => DocumentType.Newspaper,
                    "pdf" => DocumentType.Pdf,
                    "research" => DocumentType.Research,
                    "article" => DocumentType.Research, // Article maps to Research
                    _ => DocumentType.Others
                };

                var documents = await _context.Documents
                    .Include(d => d.DocumentStorage)
                    .Where(d => d.UserId == userId && 
                               d.Status == DocumentStatus.Saved.ToString() && 
                               d.DocumentType == (int)documentType)
                    .OrderByDescending(d => d.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return documents;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting documents by folder: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a paginated list of documents by folder type along with the total count.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="folderType">The type of folder (e.g., "Book", "Word").</param>
        /// <param name="page">The page number.</param>
        /// <param name="pageSize">The number of documents per page.</param>
        /// <returns>A tuple containing a list of <see cref="Documents"/> and the total count of documents.</returns>
        public async Task<(List<Documents> documents, int totalCount)> GetDocumentsByFolderWithCountAsync(int userId, string folderType, int page = 1, int pageSize = 10)
        {
            try
            {
                // Map folder type to document type enum
                var documentType = folderType.ToLower() switch
                {
                    "book" => DocumentType.Book,
                    "word" => DocumentType.Word, 
                    "newspaper" => DocumentType.Newspaper,
                    "pdf" => DocumentType.Pdf,
                    "research" => DocumentType.Research,
                    "article" => DocumentType.Research, // Article maps to Research
                    _ => DocumentType.Others
                };

                var query = _context.Documents
                    .Include(d => d.DocumentStorage)
                    .Where(d => d.UserId == userId && 
                               d.Status == DocumentStatus.Saved.ToString() && 
                               d.DocumentType == (int)documentType);

                var totalCount = await query.CountAsync();

                var documents = await query
                    .OrderByDescending(d => d.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (documents, totalCount);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting documents by folder with count: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a summary of document counts for each folder type for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A dictionary where the key is the folder type (string) and the value is the count of documents.</returns>
        public async Task<Dictionary<string, int>> GetFolderSummaryAsync(int userId)
        {
            try
            {
                var folderCounts = await _context.Documents
                    .Where(d => d.UserId == userId && d.Status == DocumentStatus.Saved.ToString())
                    .GroupBy(d => d.DocumentType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToListAsync();

                var summary = new Dictionary<string, int>
                {
                    { "Book", 0 },
                    { "Word", 0 },
                    { "Newspaper", 0 },
                    { "PDF", 0 },
                    { "Research", 0 },
                    { "Others", 0 }
                };

                foreach (var item in folderCounts)
                {
                    var folderName = ((DocumentType)item.Type) switch
                    {
                        DocumentType.Book => "Book",
                        DocumentType.Word => "Word",
                        DocumentType.Newspaper => "Newspaper", 
                        DocumentType.Pdf => "PDF",
                        DocumentType.Research => "Research",
                        _ => "Others"
                    };

                    if (summary.ContainsKey(folderName))
                    {
                        summary[folderName] = item.Count;
                    }
                }

                return summary;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting folder summary: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up expired documents from the trash.
        /// </summary>
        /// <returns>True if the cleanup was successful, false otherwise.</returns>
        public async Task<bool> CleanExpiredTrashAsync()
        {
            try
            {
                var expiredDocuments = await _context.Documents
                    .Include(d => d.DocumentStorage)
                    .Include(d => d.Citations)
                    .Where(d => d.Status == DocumentStatus.Trash.ToString() && 
                               d.TrashDate.HasValue && 
                               d.TrashDate.Value <= DateTime.UtcNow)
                    .ToListAsync();

                foreach (var document in expiredDocuments)
                {
                    // Xóa tất cả related records
                    _context.DocumentStorage.RemoveRange(document.DocumentStorage);
                    _context.Citations.RemoveRange(document.Citations);
                    _context.Documents.Remove(document);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error cleaning expired trash: {ex.Message}");
            }
        }

        /// <summary>
        /// Automatically cleans up expired documents from the trash (background service).
        /// </summary>
        /// <returns>True if the auto-cleanup was successful, false otherwise.</returns>
        public async Task<bool> AutoCleanTrashAsync()
        {
            try
            {
                // Background service để tự động xóa documents trong trash sau 10 ngày
                var expiredDocuments = await _context.Documents
                    .Include(d => d.DocumentStorage)
                    .Include(d => d.Citations)
                    .Where(d => d.Status == DocumentStatus.Trash.ToString() && 
                               d.TrashDate.HasValue && 
                               d.TrashDate.Value <= DateTime.UtcNow)
                    .ToListAsync();

                if (expiredDocuments.Any())
                {
                    foreach (var document in expiredDocuments)
                    {
                        // Xóa tất cả related records
                        _context.DocumentStorage.RemoveRange(document.DocumentStorage);
                        _context.Citations.RemoveRange(document.Citations);
                        _context.Documents.Remove(document);
                    }

                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in auto clean trash: {ex.Message}");
            }
        }


        /// <summary>
        /// Updates document info (Title, Author, Publisher, Year).
        /// </summary>
        /// <param name="request">The document update request.</param>
        /// <returns>True if updated successfully, otherwise false.</returns>
        public async Task<bool> UpdateDocumentInfoAsync(DocumentUpdateRequest request)
        {
            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.DocumentId == request.DocumentId && d.UserId == request.UserId);

            if (document == null)
                return false;

            // Cập nhật các trường được phép chỉnh sửa
            if (!string.IsNullOrEmpty(request.Title))
                document.Title = request.Title;

            if (!string.IsNullOrEmpty(request.Author))
                document.Author = request.Author;

            if (!string.IsNullOrEmpty(request.Publisher))
                document.Publisher = request.Publisher;

            if (request.Year.HasValue)
                document.Year = request.Year.Value;

            if (!string.IsNullOrEmpty(request.Doi))
                document.Doi = request.Doi;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
