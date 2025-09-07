using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.Enum;

namespace VUniBox.Services.Classification
{
    public class FileClassificationService : IFileClassificationService
    {
        private readonly Dictionary<string, DocumentType> _fileExtensionMapping;

        public FileClassificationService()
        {
            _fileExtensionMapping = new Dictionary<string, DocumentType>(StringComparer.OrdinalIgnoreCase)
            {
                // Word documents
                { ".doc", DocumentType.Word },
                { ".docx", DocumentType.Word },
                { ".rtf", DocumentType.Word },
                
                // PDF documents
                { ".pdf", DocumentType.Pdf },
                
                // PowerPoint
                { ".ppt", DocumentType.Others },
                { ".pptx", DocumentType.Others },
                
                // Excel
                { ".xls", DocumentType.Others },
                { ".xlsx", DocumentType.Others },
                
                // Text files
                { ".txt", DocumentType.Others },
                { ".md", DocumentType.Others },
                
                // Images
                { ".jpg", DocumentType.Others },
                { ".jpeg", DocumentType.Others },
                { ".png", DocumentType.Others },
                { ".gif", DocumentType.Others },
                
                // Archives
                { ".zip", DocumentType.Others },
                { ".rar", DocumentType.Others },
                { ".7z", DocumentType.Others }
            };
        }

        public async Task<ClassificationResponse> ClassifyFileAsync(string fileName)
        {
            return await Task.FromResult(ClassifyFile(fileName));
        }

        public ClassificationResponse ClassifyFile(string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return new ClassificationResponse
                    {
                        Success = false,
                        DocumentType = DocumentType.Others,
                        TypeName = "Others",
                        Message = "File name is empty or null"
                    };
                }

                // Extract file extension
                var extension = Path.GetExtension(fileName);
                
                if (string.IsNullOrEmpty(extension))
                {
                    return new ClassificationResponse
                    {
                        Success = true,
                        DocumentType = DocumentType.Others,
                        TypeName = "Others",
                        Message = "No file extension found, classified as Others"
                    };
                }

                // Check if extension exists in mapping
                if (_fileExtensionMapping.TryGetValue(extension, out var documentType))
                {
                    return new ClassificationResponse
                    {
                        Success = true,
                        DocumentType = documentType,
                        TypeName = documentType.ToString(),
                        Message = $"File classified as {documentType} based on extension {extension}"
                    };
                }

                // Default to Others if extension not recognized
                return new ClassificationResponse
                {
                    Success = true,
                    DocumentType = DocumentType.Others,
                    TypeName = "Others",
                    Message = $"Unknown file extension {extension}, classified as Others"
                };
            }
            catch (Exception ex)
            {
                return new ClassificationResponse
                {
                    Success = false,
                    DocumentType = DocumentType.Others,
                    TypeName = "Others",
                    Message = $"Error classifying file: {ex.Message}"
                };
            }
        }
    }
}








