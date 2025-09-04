using VUniBox.Models;

namespace VUniBox.Models.DTO.Response
{
    public class TrashResponse
    {
        public bool Success { get; set; }
        public List<Documents> TrashDocuments { get; set; } = new();
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public int TotalCount { get; set; }
    }
}

