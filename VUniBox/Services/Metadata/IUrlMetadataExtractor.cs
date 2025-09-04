using VUniBox.Models.DTO;
using VUniBox.Models.Enum;

namespace VUniBox.Services.Metadata
{
    public interface IUrlMetadataExtractor
    {
        Task<DocumentMetadataDto> ExtractMetadataAsync(string url, DocumentType documentType);
        Task<DocumentMetadataDto> ExtractFromResearchSiteAsync(string url);
        Task<DocumentMetadataDto> ExtractFromBookSiteAsync(string url);
        Task<DocumentMetadataDto> ExtractFromNewsSiteAsync(string url);
        Task<DocumentMetadataDto> ExtractGenericMetadataAsync(string url);
    }
}
