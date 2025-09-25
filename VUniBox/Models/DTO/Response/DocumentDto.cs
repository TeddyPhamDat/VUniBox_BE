namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// DTO to prevent circular reference issues when returning Documents entity
    /// Contains all document fields without navigation properties
    /// </summary>
    public class DocumentDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the document.
        /// </summary>
        public int DocumentId { get; set; }
        /// <summary>
        /// Gets or sets the unique identifier of the user who owns the document.
        /// </summary>
        public int UserId { get; set; }
        /// <summary>
        /// Gets or sets the title of the document.
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the file path of the document.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the source URL of the document.
        /// </summary>
        public string SourceUrl { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the type of the document.
        /// </summary>
        public int Type { get; set; } 
        /// <summary>
        /// Gets or sets the status of the document.
        /// </summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the creation date and time of the document.
        /// </summary>
        public DateTime? CreatedAt { get; set; }
        /// <summary>
        /// Gets or sets the expiry date of the document (e.g., when it will be permanently deleted from trash).
        /// </summary>
        public DateTime? ExpiryDate { get; set; }
        /// <summary>
        /// Gets or sets the primary author of the document.
        /// </summary>
        public string Author { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets a comma-separated list of all authors of the document.
        /// </summary>
        public string Authors { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the publication date of the document.
        /// </summary>
        public DateOnly? PublicationDate { get; set; }
        /// <summary>
        /// Gets or sets the publisher of the document.
        /// </summary>
        public string Publisher { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the journal where the document was published, if applicable.
        /// </summary>
        public string Journal { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the volume number of the publication, if applicable.
        /// </summary>
        public string Volume { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the issue number of the publication, if applicable.
        /// </summary>
        public string Issue { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the page numbers of the document within a publication, if applicable.
        /// </summary>
        public string Pages { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the Digital Object Identifier (DOI) of the document.
        /// </summary>
        public string Doi { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the International Standard Book Number (ISBN) of the document.
        /// </summary>
        public string Isbn { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the abstract or summary of the document content.
        /// </summary>
        public string Abstract { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets a general description of the document.
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets keywords associated with the document.
        /// </summary>
        public string Keywords { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the subject area or category of the document.
        /// </summary>
        public string Subject { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the source of the document.
        /// </summary>
        public string Source { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the language of the document.
        /// </summary>
        public string Language { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the date and time when the document was retrieved.
        /// </summary>
        public DateTime? RetrievedDate { get; set; }
        /// <summary>
        /// Gets or sets the citation style for the document.
        /// </summary>
        public string CitationStyle { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentDto"/> class.
        /// </summary>
        public DocumentDto() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentDto"/> class from a <see cref="Documents"/> entity.
        /// </summary>
        /// <param name="document">The <see cref="Documents"/> entity to convert from.</param>
        public DocumentDto(Documents document)
        {
            DocumentId = document.DocumentId;
            UserId = document.UserId;
            Title = document.Title ?? string.Empty;
            FilePath = document.FilePath ?? string.Empty;
            SourceUrl = document.SourceUrl ?? string.Empty;
            Type = document.DocumentType;
            Status = document.Status ?? string.Empty;
            CreatedAt = document.CreatedAt;
            ExpiryDate = document.TrashDate;
            Author = document.Author ?? string.Empty;
            Authors = document.Authors ?? string.Empty;
            PublicationDate = document.PublicationDate;
            Publisher = document.Publisher ?? string.Empty;
            Journal = document.Journal ?? string.Empty;
            Volume = document.Volume ?? string.Empty;
            Issue = document.Issue ?? string.Empty;
            Pages = document.Pages ?? string.Empty;
            Doi = document.Doi ?? string.Empty;
            Isbn = document.Isbn ?? string.Empty;
            Abstract = document.Abstract ?? string.Empty;
            Description = document.Description ?? string.Empty;
            Keywords = document.Keywords ?? string.Empty;
            Subject = document.Subject ?? string.Empty;
            Source = document.Source ?? string.Empty;
            Language = document.Language ?? string.Empty;
            RetrievedDate = document.RetrievedDate;
            CitationStyle = document.CitationStyle ?? string.Empty;
        }
    }
}