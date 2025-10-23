namespace VUniBox.Models.DTO.Request
{
    public class DocumentUpdateRequest
    {
        public int DocumentId { get; set; }
        public int UserId { get; set; }  // để đảm bảo người dùng chỉ update tài liệu của mình
        public string? Title { get; set; }
        public int? Year { get; set; }
        public string? Author { get; set; }
        public string? Publisher { get; set; }
        public string? Doi { get; set; }
        public string? Volume { get; set; }
        public string? Issue { get; set; }
        public string? Pages { get; set; }
    }
}
