using VUniBox.Models;

namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Represents the response for a request to retrieve documents from the trash.
    /// </summary>
    public class TrashResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the list of documents currently in the trash.
        /// </summary>
        public List<Documents> TrashDocuments { get; set; } = new();
        /// <summary>
        /// Gets or sets a message related to the operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets an error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }
        /// <summary>
        /// Gets or sets the total count of documents in the trash.
        /// </summary>
        public int TotalCount { get; set; }
    }
}









