using System.Threading.Tasks;

namespace VUniBox.Services.Citation
{
    public interface IGeminiCitationService
    {
        Task<(string formatted, string inText)> GenerateCitationAsync(
            string title,
            string authors,
            int? year, // Change to int? to allow nulls
            string publicationDate, // New parameter
            string type,
            string url,
            string style);

        Task<string> ExtractAuthorWithAIAsync(string prompt);
        Task<(string author, int? year, string publisher)> ExtractCitationMetadataWithAIAsync(string prompt);
    }
}
