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
        public int UserId { get; set; }
        public DocumentMetadataDto Metadata { get; set; } = new();
        public DocumentType DocumentType { get; set; }
        public string? FilePath { get; set; }
        public bool SaveToFolder { get; set; } // true = save, false = trash
    }
}