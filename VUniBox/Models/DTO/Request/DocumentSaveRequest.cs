using VUniBox.Models.Enum;
using VUniBox.Models.DTO.Response;

namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Represents a request to save a document, including its metadata and classification.
    /// </summary>
    public class DocumentSaveRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the user saving the document.
        /// </summary>
        public int UserId { get; set; }
        /// <summary>
        /// Gets or sets the metadata of the document.
        /// </summary>
        public DocumentMetadataDto Metadata { get; set; } = new();
        /// <summary>
        /// Gets or sets the classified type of the document.
        /// </summary>
        public DocumentType DocumentType { get; set; }
        /// <summary>
        /// Gets or sets the file path of the document, if it's a file upload.
        /// </summary>
        public string? FilePath { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the document should be saved to a folder (true) or moved to trash (false).
        /// </summary>
        public bool SaveToFolder { get; set; } = true; // true = save, false = move to trash
    }
}
