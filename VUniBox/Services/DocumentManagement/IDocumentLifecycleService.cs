using VUniBox.Models;
using VUniBox.Models.DTO;
using VUniBox.Models.Enum;

namespace VUniBox.Services.DocumentManagement
{
    /// <summary>
    /// Defines the contract for document lifecycle management services.
    /// </summary>
    public interface IDocumentLifecycleService
    {
        /// <summary>
        /// Saves a new document or updates an existing one.
        /// </summary>
        /// <param name="userId">The ID of the user owning the document.</param>
        /// <param name="metadata">The metadata of the document.</param>
        /// <param name="documentType">The type of the document.</param>
        /// <param name="filePath">Optional: The file path of the document.</param>
        /// <returns>The saved <see cref="Documents"/> object.</returns>
        Task<Documents> SaveDocumentAsync(int userId, DocumentMetadataDto metadata, DocumentType documentType, string? filePath = null);
        /// <summary>
        /// Retrieves a document by its ID.
        /// </summary>
        /// <param name="documentId">The ID of the document.</param>
        /// <returns>A <see cref="Documents"/> object if found, otherwise null.</returns>
        Task<Documents?> GetDocumentByIdAsync(int documentId);
        /// <summary>
        /// Moves a document to the trash.
        /// </summary>
        /// <param name="documentId">The ID of the document to move.</param>
        /// <param name="userId">The ID of the user owning the document.</param>
        /// <returns>True if the document was successfully moved to trash, false otherwise.</returns>
        Task<bool> MoveToTrashAsync(int documentId, int userId);
        /// <summary>
        /// Restores a document from the trash.
        /// </summary>
        /// <param name="documentId">The ID of the document to restore.</param>
        /// <param name="userId">The ID of the user owning the document.</param>
        /// <returns>True if the document was successfully restored, false otherwise.</returns>
        Task<bool> RestoreFromTrashAsync(int documentId, int userId);
        /// <summary>
        /// Deletes a document permanently.
        /// </summary>
        /// <param name="documentId">The ID of the document to delete.</param>
        /// <param name="userId">The ID of the user owning the document.</param>
        /// <returns>True if the document was successfully deleted, false otherwise.</returns>
        Task<bool> DeletePermanentlyAsync(int documentId, int userId);
        /// <summary>
        /// Retrieves a list of documents in the trash for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A list of <see cref="Documents"/> in the trash.</returns>
        Task<List<Documents>> GetTrashDocumentsAsync(int userId);
        /// <summary>
        /// Retrieves a list of saved documents for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A list of saved <see cref="Documents"/>.</returns>
        Task<List<Documents>> GetSavedDocumentsAsync(int userId);
        /// <summary>
        /// Retrieves all documents (saved and trash) for a user with optional filtering.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="status">Optional: The document status to filter by.</param>
        /// <param name="type">Optional: The document type to filter by.</param>
        /// <returns>A list of filtered <see cref="Documents"/>.</returns>
        Task<List<Documents>> GetAllDocumentsAsync(int userId, string? status = null, string? type = null);
        /// <summary>
        /// Retrieves a paginated list of documents by folder type.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="folderType">The type of folder (e.g., "Book", "Word").</param>
        /// <param name="page">The page number.</param>
        /// <param name="pageSize">The number of documents per page.</param>
        /// <returns>A paginated list of <see cref="Documents"/> in the specified folder.</returns>
        Task<List<Documents>> GetDocumentsByFolderAsync(int userId, string folderType, int page = 1, int pageSize = 10);
        /// <summary>
        /// Retrieves a paginated list of documents by folder type along with the total count.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="folderType">The type of folder (e.g., "Book", "Word").</param>
        /// <param name="page">The page number.</param>
        /// <param name="pageSize">The number of documents per page.</param>
        /// <returns>A tuple containing a list of <see cref="Documents"/> and the total count of documents.</returns>
        Task<(List<Documents> documents, int totalCount)> GetDocumentsByFolderWithCountAsync(int userId, string folderType, int page = 1, int pageSize = 10);
        /// <summary>
        /// Retrieves a summary of document counts for each folder type for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A dictionary where the key is the folder type (string) and the value is the count of documents.</returns>
        Task<Dictionary<string, int>> GetFolderSummaryAsync(int userId);
        /// <summary>
        /// Cleans up expired documents from the trash.
        /// </summary>
        /// <returns>True if the cleanup was successful, false otherwise.</returns>
        Task<bool> CleanExpiredTrashAsync();
        /// <summary>
        /// Automatically cleans up expired documents from the trash (background service).
        /// </summary>
        /// <returns>True if the auto-cleanup was successful, false otherwise.</returns>
        Task<bool> AutoCleanTrashAsync();
    }
}
