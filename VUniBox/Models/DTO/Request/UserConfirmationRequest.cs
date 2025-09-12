using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Request for user confirmation after classification
    /// Used when user confirms their decision to save or trash a document
    /// after auto-classification step
    /// </summary>
    public class UserConfirmationRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the user making the confirmation.
        /// </summary>
        public int UserId { get; set; }
        /// <summary>
        /// Gets or sets the temporary file path of the document, if applicable.
        /// </summary>
        public string? FilePath { get; set; }
        /// <summary>
        /// Gets or sets the original file name of the document, if applicable.
        /// </summary>
        public string? FileName { get; set; }
        /// <summary>
        /// Gets or sets the URL of the document, if applicable.
        /// </summary>
        public string? Url { get; set; }
        /// <summary>
        /// Gets or sets the classified type of the document.
        /// </summary>
        public DocumentType DocumentType { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the document should be saved to a folder (true) or moved to trash (false).
        /// </summary>
        public bool SaveToFolder { get; set; } // true = save to folder, false = move to trash
        /// <summary>
        /// Gets or sets a temporary ID for tracking the session.
        /// </summary>
        public string TempId { get; set; } = string.Empty; // For tracking the session
    }
}