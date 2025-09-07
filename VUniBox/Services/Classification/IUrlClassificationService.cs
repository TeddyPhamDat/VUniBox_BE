using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;

namespace VUniBox.Services.Classification
{
    public interface IUrlClassificationService
    {
        Task<ClassificationResponse> ClassifyUrlAsync(string url);
        ClassificationResponse ClassifyUrl(string url);
    }
}


