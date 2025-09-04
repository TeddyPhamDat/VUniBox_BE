using Microsoft.EntityFrameworkCore;
using VUniBox.DBContext;
using VUniBox.Models;
using VUniBox.Models.DTO;
using VUniBox.Models.Enum;

namespace VUniBox.Services.DocumentManagement
{
    public class DocumentLifecycleService : IDocumentLifecycleService
    {
        private readonly VUniBoxContext _context;

        public DocumentLifecycleService(VUniBoxContext context)
        {
            _context = context;
        }

        public async Task<Documents> SaveDocumentAsync(int userId, DocumentMetadataDto metadata, DocumentType documentType, string? filePath = null)
        {
            try
            {
                // Tạo document record
                var document = new Documents
                {
                    UserId = userId,
                    Title = metadata.Title,
                    FilePath = filePath ?? metadata.FilePath,
                    SourceUrl = metadata.URL,
                    Type = documentType.ToString(),
                    Status = DocumentStatus.Saved.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    ExpiryDate = null, // Không có expiry date cho saved documents
                    
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
                        PublicYear = metadata.PublicationDate?.Year,
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
                
                return document;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving document: {ex.Message}");
            }
        }

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
                document.ExpiryDate = DateTime.UtcNow.AddDays(10);

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error moving document to trash: {ex.Message}");
            }
        }

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
                document.ExpiryDate = null;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error restoring document from trash: {ex.Message}");
            }
        }

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
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting document permanently: {ex.Message}");
            }
        }

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

        public async Task<bool> CleanExpiredTrashAsync()
        {
            try
            {
                var expiredDocuments = await _context.Documents
                    .Include(d => d.DocumentStorage)
                    .Include(d => d.Citations)
                    .Where(d => d.Status == DocumentStatus.Trash.ToString() && 
                               d.ExpiryDate.HasValue && 
                               d.ExpiryDate.Value <= DateTime.UtcNow)
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

        public async Task<bool> AutoCleanTrashAsync()
        {
            try
            {
                // Background service để tự động xóa documents trong trash sau 10 ngày
                var expiredDocuments = await _context.Documents
                    .Include(d => d.DocumentStorage)
                    .Include(d => d.Citations)
                    .Where(d => d.Status == DocumentStatus.Trash.ToString() && 
                               d.ExpiryDate.HasValue && 
                               d.ExpiryDate.Value <= DateTime.UtcNow)
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
    }
}
