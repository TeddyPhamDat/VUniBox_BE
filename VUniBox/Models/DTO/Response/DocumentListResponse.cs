using VUniBox.Models.DTO.Response;

namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Represents the response for a request to retrieve saved documents.
    /// </summary>
    public class SavedDocumentsResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the list of saved documents.
        /// </summary>
        public List<DocumentDto> SavedDocuments { get; set; } = new List<DocumentDto>();
        /// <summary>
        /// Gets or sets a message related to the operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets an error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }
        /// <summary>
        /// Gets or sets the total count of saved documents.
        /// </summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Represents the response for a request to retrieve all documents.
    /// </summary>
    public class AllDocumentsResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the list of documents.
        /// </summary>
        public List<DocumentDto> Documents { get; set; } = new List<DocumentDto>();
        /// <summary>
        /// Gets or sets a message related to the operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets an error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }
        /// <summary>
        /// Gets or sets the total count of documents.
        /// </summary>
        public int TotalCount { get; set; }
        /// <summary>
        /// Gets or sets the filter criteria applied to the document list.
        /// </summary>
        public object? FilteredBy { get; set; }
    }

    /// <summary>
    /// Represents the response for a request to retrieve documents by folder type.
    /// </summary>
    public class FolderDocumentsResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the type of the folder.
        /// </summary>
        public string FolderType { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the list of documents with metadata.
        /// </summary>
        public List<DocumentWithMetadataDto> Documents { get; set; } = new List<DocumentWithMetadataDto>();
        /// <summary>
        /// Gets or sets a message related to the operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets an error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }
        /// <summary>
        /// Gets or sets the total count of documents in the folder.
        /// </summary>
        public int TotalCount { get; set; }
        /// <summary>
        /// Gets or sets the current page number.
        /// </summary>
        public int CurrentPage { get; set; }
        /// <summary>
        /// Gets or sets the page size.
        /// </summary>
        public int PageSize { get; set; }
    }

    /// <summary>
    /// Represents the response for a request to retrieve documents by folder type with citation information.
    /// </summary>
    public class FolderDocumentsWithCitationResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the type of the folder.
        /// </summary>
        public string FolderType { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the list of documents with complete metadata and citations.
        /// </summary>
        public List<DocumentWithCitationDto> Documents { get; set; } = new List<DocumentWithCitationDto>();
        /// <summary>
        /// Gets or sets a message related to the operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets an error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }
        /// <summary>
        /// Gets or sets the total count of documents in the folder.
        /// </summary>
        public int TotalCount { get; set; }
        /// <summary>
        /// Gets or sets the current page number.
        /// </summary>
        public int CurrentPage { get; set; }
        /// <summary>
        /// Gets or sets the page size.
        /// </summary>
        public int PageSize { get; set; }
    }

    /// <summary>
    /// Represents the response for a request to retrieve a folder summary.
    /// </summary>
    public class FolderSummaryResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the dictionary of folder counts, where the key is the folder type and the value is the count.
        /// </summary>
        public Dictionary<string, int> FolderCounts { get; set; } = new Dictionary<string, int>();
        /// <summary>
        /// Gets or sets a message related to the operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets an error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }
        /// <summary>
        /// Gets or sets the total number of documents across all folders.
        /// </summary>
        public int TotalDocuments { get; set; }
    }

    /// <summary>
    /// Represents a document with additional metadata.
    /// </summary>
    public class DocumentWithMetadataDto : DocumentDto
    {
        /// <summary>
        /// Gets or sets the size of the file in bytes.
        /// </summary>
        public long? FileSize { get; set; }
        /// <summary>
        /// Gets or sets the type of the file (e.g., "pdf", "docx").
        /// </summary>
        public string? FileType { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentWithMetadataDto"/> class.
        /// </summary>
        public DocumentWithMetadataDto() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentWithMetadataDto"/> class with document data.
        /// </summary>
        /// <param name="document">The document entity.</param>
        public DocumentWithMetadataDto(Documents document) : base(document)
        {
            // Get metadata from DocumentStorage if available
            var storage = document.DocumentStorage?.FirstOrDefault();
            if (storage != null)
            {
                FileSize = storage.FileSize;
                FileType = storage.FileType;
            }
        }
    }
}
