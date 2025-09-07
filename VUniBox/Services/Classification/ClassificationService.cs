using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;

namespace VUniBox.Services.Classification
{
    public class ClassificationService : IClassificationService
    {
        private readonly IFileClassificationService _fileClassificationService;
        private readonly IUrlClassificationService _urlClassificationService;

        public ClassificationService(
            IFileClassificationService fileClassificationService,
            IUrlClassificationService urlClassificationService)
        {
            _fileClassificationService = fileClassificationService;
            _urlClassificationService = urlClassificationService;
        }

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








