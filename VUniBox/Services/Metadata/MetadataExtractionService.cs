using VUniBox.Models.DTO;
using VUniBox.Models.Enum;

namespace VUniBox.Services.Metadata
{
    /// <summary>
    /// Service for extracting metadata from various input types (files, URLs).
    /// </summary>
    public class MetadataExtractionService : IMetadataExtractionService
    {
        private readonly IFileMetadataExtractor _fileMetadataExtractor;
        private readonly IUrlMetadataExtractor _urlMetadataExtractor;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataExtractionService"/> class.
        /// </summary>
        /// <param name="fileMetadataExtractor">The file metadata extractor.</param>
        /// <param name="urlMetadataExtractor">The URL metadata extractor.</param>
        public MetadataExtractionService(
            IFileMetadataExtractor fileMetadataExtractor,
            IUrlMetadataExtractor urlMetadataExtractor)
        {
            _fileMetadataExtractor = fileMetadataExtractor;
            _urlMetadataExtractor = urlMetadataExtractor;
        }

        /// <summary>
        /// Extracts metadata from the given input based on its type.
        /// </summary>
        /// <param name="input">The input string (file path or URL).</param>
        /// <param name="inputType">The type of the input (File or Url).</param>
        /// <param name="documentType">The classified document type.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata.</returns>
        /// <exception cref="ArgumentException">Thrown when an invalid input type is provided.</exception>
        public async Task<DocumentMetadataDto> ExtractMetadataAsync(string input, InputType inputType, DocumentType documentType)
        {
            return inputType switch
            {
                InputType.File => await ExtractFromFileAsync(input, Path.GetFileName(input), documentType),
                InputType.Url => await ExtractFromUrlAsync(input, documentType),
                _ => throw new ArgumentException("Invalid input type")
            };
        }

        /// <summary>
        /// Extracts metadata from a file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="fileName">The name of the file.</param>
        /// <param name="documentType">The classified document type.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata from the file.</returns>
        public async Task<DocumentMetadataDto> ExtractFromFileAsync(string filePath, string fileName, DocumentType documentType)
        {
            try
            {
                var metadata = await _fileMetadataExtractor.ExtractMetadataAsync(filePath, fileName);
                
                // Set document type
                metadata.FileType = Path.GetExtension(fileName);
                
                // Add classification information
                if (string.IsNullOrEmpty(metadata.Description))
                {
                    metadata.Description = $"Document classified as {documentType}";
                }
                else
                {
                    metadata.Description += $". Document classified as {documentType}";
                }

                return metadata;
            }
            catch (Exception ex)
            {
                return new DocumentMetadataDto
                {
                    FilePath = filePath,
                    Title = Path.GetFileNameWithoutExtension(fileName),
                    Description = $"Error extracting file metadata: {ex.Message}",
                    FileType = Path.GetExtension(fileName)
                };
            }
        }

        /// <summary>
        /// Extracts metadata from a URL.
        /// </summary>
        /// <param name="url">The URL to extract metadata from.</param>
        /// <param name="documentType">The classified document type.</param>
        /// <returns>A <see cref="DocumentMetadataDto"/> containing the extracted metadata from the URL.</returns>
        public async Task<DocumentMetadataDto> ExtractFromUrlAsync(string url, DocumentType documentType)
        {
            try
            {
                var metadata = await _urlMetadataExtractor.ExtractMetadataAsync(url, documentType);
                
                // Add classification information
                if (string.IsNullOrEmpty(metadata.Description))
                {
                    metadata.Description = $"URL classified as {documentType}";
                }
                else
                {
                    metadata.Description += $". URL classified as {documentType}";
                }

                return metadata;
            }
            catch (Exception ex)
            {
                return new DocumentMetadataDto
                {
                    URL = url,
                    Title = "Error extracting URL metadata",
                    Description = $"Error extracting URL metadata: {ex.Message}"
                };
            }
        }
    }
}
