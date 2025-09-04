namespace VUniBox.Models.DTO
{
    public class DocumentMetadataDto
    {
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? Authors { get; set; } // Multiple authors separated by comma
        public DateOnly? PublicationDate { get; set; }
        public string? Publisher { get; set; }
        public string? Journal { get; set; }
        public string? Volume { get; set; }
        public string? Issue { get; set; }
        public string? Pages { get; set; }
        public string? DOI { get; set; }
        public string? ISBN { get; set; }
        public string? URL { get; set; }
        public string? Abstract { get; set; }
        public string? Description { get; set; }
        public string? Language { get; set; } = "en";
        public string? Source { get; set; } // Website name or source
        public string? FilePath { get; set; }
        public long? FileSize { get; set; }
        public string? FileType { get; set; }
        public DateTime? RetrievedDate { get; set; } = DateTime.UtcNow;
        public string? Keywords { get; set; }
        public string? Subject { get; set; }
        public string? Rights { get; set; }
        public string? License { get; set; }
    }
}
