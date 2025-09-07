using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;

namespace VUniBox.Services.Classification
{
    public interface IFileClassificationService
    {
        Task<ClassificationResponse> ClassifyFileAsync(string fileName);
        ClassificationResponse ClassifyFile(string fileName);
    }
}


