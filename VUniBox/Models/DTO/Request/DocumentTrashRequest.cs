namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Represents a request to move a document to trash or restore it from trash.
    /// </summary>
    public class DocumentTrashRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the document.
        /// </summary>
        public int DocumentId { get; set; }
        /// <summary>
        /// Gets or sets the unique identifier of the user who owns the document.
        /// </summary>
        public int UserId { get; set; }
    }
}






