using VUniBox.Models.DTO;
using VUniBox.Models.Enum;

namespace VUniBox.Services.Metadata
{
    /// <summary>
    /// Defines the contract for URL metadata extraction services.
    /// </summary>
    public interface IUrlMetadataExtractor
    {
        /// <summary>
        /// Extracts metadata from a given URL based on the specified document type.
        /// </summary>
        /// <param name="url">The URL to extract metadata from.</param>
        /// <param name="documentType">The classified document type.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata.</returns>
        Task<DocumentMetadataDto> ExtractMetadataAsync(string url, DocumentType documentType);
        /// <summary>
        /// Extracts metadata specifically from a research-oriented website.
        /// </summary>
        /// <param name="url">The URL of the research site.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata.</returns>
        Task<DocumentMetadataDto> ExtractFromResearchSiteAsync(string url);
        /// <summary>
        /// Extracts metadata specifically from a book-related website.
        /// </summary>
        /// <param name="url">The URL of the book site.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata.</returns>
        Task<DocumentMetadataDto> ExtractFromBookSiteAsync(string url);
        /// <summary>
        /// Extracts metadata specifically from a news-related website.
        /// </summary>
        /// <param name="url">The URL of the news site.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata.</returns>
        Task<DocumentMetadataDto> ExtractFromNewsSiteAsync(string url);
        /// <summary>
        /// Extracts generic metadata from any given URL by parsing common HTML meta tags.
        /// </summary>
        /// <param name="url">The URL to extract metadata from.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted generic metadata.</returns>
        Task<DocumentMetadataDto> ExtractGenericMetadataAsync(string url);
    }
}
