using VUniBox.Models.DTO;
using VUniBox.Models.Enum;

namespace VUniBox.Services.Metadata
{
    public class MetadataExtractionService : IMetadataExtractionService
    {
        private readonly IFileMetadataExtractor _fileMetadataExtractor;
        private readonly IUrlMetadataExtractor _urlMetadataExtractor;

        public MetadataExtractionService(
            IFileMetadataExtractor fileMetadataExtractor,
            IUrlMetadataExtractor urlMetadataExtractor)
        {
            _fileMetadataExtractor = fileMetadataExtractor;
            _urlMetadataExtractor = urlMetadataExtractor;
        }

        public async Task<DocumentMetadataDto> ExtractMetadataAsync(string input, InputType inputType, DocumentType documentType)
        {
            return inputType switch
            {
                InputType.File => await ExtractFromFileAsync(input, Path.GetFileName(input), documentType),
                InputType.Url => await ExtractFromUrlAsync(input, documentType),
                _ => throw new ArgumentException("Invalid input type")
            };
        }

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
