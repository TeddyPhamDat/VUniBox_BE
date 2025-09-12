using VUniBox.Models;
using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Response for document save operations
    /// Used when saving documents to folder or moving to trash
    /// </summary>
    public class DocumentSaveResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the document save operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the document data transfer object, if the operation was successful.
        /// </summary>
        public DocumentDto? Document { get; set; }
        /// <summary>
        /// Gets or sets a message describing the result of the document save operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets an error message if the document save operation failed.
        /// </summary>
        public string? Error { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the document is in the trash.
        /// </summary>
        public bool IsInTrash { get; set; } = false;
        /// <summary>
        /// Gets or sets the expiry date of the document if it is in the trash.
        /// </summary>
        public DateTime? ExpiryDate { get; set; }
    }
}









