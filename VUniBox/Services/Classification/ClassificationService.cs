using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;

namespace VUniBox.Services.Classification
{
    /// <summary>
    /// Service for classifying documents based on their input type (file or URL).
    /// </summary>
    public class ClassificationService : IClassificationService
    {
        private readonly IFileClassificationService _fileClassificationService;
        private readonly IUrlClassificationService _urlClassificationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClassificationService"/> class.
        /// </summary>
        /// <param name="fileClassificationService">The file classification service.</param>
        /// <param name="urlClassificationService">The URL classification service.</param>
        public ClassificationService(
            IFileClassificationService fileClassificationService,
            IUrlClassificationService urlClassificationService)
        {
            _fileClassificationService = fileClassificationService;
            _urlClassificationService = urlClassificationService;
        }

        /// <summary>
        /// Classifies an input (file or URL) and returns the classification response.
        /// </summary>
        /// <param name="request">The classification request containing input type and data.</param>
        /// <returns>A <see cref="ClassificationResponse"/> indicating the success and classified document type.</returns>
        public async Task<ClassificationResponse> ClassifyAsync(ClassificationRequest request)
        {
            try
            {
                switch (request.InputType)
                {
                    case Models.Enum.InputType.File:
                        if (string.IsNullOrWhiteSpace(request.FileName))
                        {
                            return new ClassificationResponse
                            {
                                Success = false,
                                Message = "File name is required for file classification"
                            };
                        }
                        return await _fileClassificationService.ClassifyFileAsync(request.FileName);

                    case Models.Enum.InputType.Url:
                        if (string.IsNullOrWhiteSpace(request.Url))
                        {
                            return new ClassificationResponse
                            {
                                Success = false,
                                Message = "URL is required for URL classification"
                            };
                        }
                        return await _urlClassificationService.ClassifyUrlAsync(request.Url);

                    default:
                        return new ClassificationResponse
                        {
                            Success = false,
                            Message = "Invalid input type"
                        };
                }
            }
            catch (Exception ex)
            {
                return new ClassificationResponse
                {
                    Success = false,
                    Message = $"Error during classification: {ex.Message}"
                };
            }
        }
    }
}









