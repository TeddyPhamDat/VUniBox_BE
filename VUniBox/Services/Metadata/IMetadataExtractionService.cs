using VUniBox.Models.DTO;
using VUniBox.Models.Enum;

namespace VUniBox.Services.Metadata
{
    public interface IMetadataExtractionService
    {
        Task<DocumentMetadataDto> ExtractMetadataAsync(string input, InputType inputType, DocumentType documentType);
        Task<DocumentMetadataDto> ExtractFromFileAsync(string filePath, string fileName, DocumentType documentType);
        Task<DocumentMetadataDto> ExtractFromUrlAsync(string url, DocumentType documentType);
    }
}
