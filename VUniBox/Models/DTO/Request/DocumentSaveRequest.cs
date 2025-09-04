using VUniBox.Models.Enum;
using VUniBox.Models.DTO.Response;

namespace VUniBox.Models.DTO.Request
{
    public class DocumentSaveRequest
    {
        public int UserId { get; set; }
        public DocumentMetadataDto Metadata { get; set; } = new();
        public DocumentType DocumentType { get; set; }
        public string? FilePath { get; set; }
        public bool SaveToFolder { get; set; } = true; // true = save, false = move to trash
    }
}
