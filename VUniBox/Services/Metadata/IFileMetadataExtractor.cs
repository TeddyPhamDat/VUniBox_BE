using VUniBox.Models.DTO.Response;

namespace VUniBox.Services.Metadata
{
    public interface IFileMetadataExtractor
    {
        Task<Models.DTO.DocumentMetadataDto> ExtractMetadataAsync(string filePath, string fileName);
        Task<Models.DTO.DocumentMetadataDto> ExtractFromPdfAsync(string filePath);
        Task<Models.DTO.DocumentMetadataDto> ExtractFromWordAsync(string filePath);
        Task<Models.DTO.DocumentMetadataDto> ExtractFromTextAsync(string filePath);
    }
}
