using VUniBox.Models;
using VUniBox.Models.DTO;
using VUniBox.Models.Enum;

namespace VUniBox.Services.DocumentManagement
{
    public interface IDocumentLifecycleService
    {
        Task<Documents> SaveDocumentAsync(int userId, DocumentMetadataDto metadata, DocumentType documentType, string? filePath = null);
        Task<bool> MoveToTrashAsync(int documentId, int userId);
        Task<bool> RestoreFromTrashAsync(int documentId, int userId);
        Task<bool> DeletePermanentlyAsync(int documentId, int userId);
        Task<List<Documents>> GetTrashDocumentsAsync(int userId);
        Task<bool> CleanExpiredTrashAsync();
        Task<bool> AutoCleanTrashAsync(); // Background service để tự động xóa sau 10 ngày
    }
}
