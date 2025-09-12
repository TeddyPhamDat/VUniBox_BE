using VUniBox.Models.DTO;
using VUniBox.Models.Enum;

namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Request for user decision with pre-extracted metadata
    /// Used when metadata is already available and user needs to decide save/trash
    /// Alternative to UserConfirmationRequest when metadata is pre-populated
    /// </summary>
    public class UserDecisionRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the user making the decision.
        /// </summary>
        public int UserId { get; set; }
        /// <summary>
        /// Gets or sets the pre-extracted metadata of the document.
        /// </summary>
        public DocumentMetadataDto Metadata { get; set; } = new();
        /// <summary>
        /// Gets or sets the classified type of the document.
        /// </summary>
        public DocumentType DocumentType { get; set; }
        /// <summary>
        /// Gets or sets the file path of the document, if applicable.
        /// </summary>
        public string? FilePath { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the document should be saved to a folder (true) or moved to trash (false).
        /// </summary>
        public bool SaveToFolder { get; set; } // true = save, false = trash
    }
}