using iTextSharp.text.pdf;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.ExtendedProperties;
using System.Text.RegularExpressions;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.DTO;
using DocumentFormat.OpenXml.CustomProperties;

namespace VUniBox.Services.Metadata
{
    public class FileMetadataExtractor : IFileMetadataExtractor
    {
        public async Task<Models.DTO.DocumentMetadataDto> ExtractMetadataAsync(string filePath, string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            
            return extension switch
            {
                ".pdf" => await ExtractFromPdfAsync(filePath),
                ".docx" or ".doc" => await ExtractFromWordAsync(filePath),
                ".txt" or ".md" => await ExtractFromTextAsync(filePath),
                _ => new Models.DTO.DocumentMetadataDto
                {
                    FilePath = filePath,
                    FileType = extension,
                    Title = Path.GetFileNameWithoutExtension(fileName),
                    Description = "File type not supported for metadata extraction"
                }
            };
        }

        public async Task<Models.DTO.DocumentMetadataDto> ExtractFromPdfAsync(string filePath)
        {
            var metadata = new Models.DTO.DocumentMetadataDto
            {
                FilePath = filePath,
                FileType = ".pdf"
            };

            try
            {
                using var reader = new PdfReader(filePath);
                var info = reader.Info;

                // Extract basic metadata
                metadata.Title = info.ContainsKey("Title") ? info["Title"] : Path.GetFileNameWithoutExtension(filePath);
                metadata.Author = info.ContainsKey("Author") ? info["Author"] : "";
                metadata.Subject = info.ContainsKey("Subject") ? info["Subject"] : "";
                metadata.Keywords = info.ContainsKey("Keywords") ? info["Keywords"] : "";

                // Extract creation date
                if (info.ContainsKey("CreationDate"))
                {
                    var creationDate = info["CreationDate"];
                    if (DateOnly.TryParse(creationDate, out var date))
                    {
                        metadata.PublicationDate = date;
                    }
                }

                // Extract creator/producer
                var creator = info.ContainsKey("Creator") ? info["Creator"] : "";
                var producer = info.ContainsKey("Producer") ? info["Producer"] : "";
                metadata.Publisher = !string.IsNullOrEmpty(creator) ? creator : producer;

                // Get file size
                var fileInfo = new FileInfo(filePath);
                metadata.FileSize = fileInfo.Length;

                // Try to extract text content for additional metadata
                try
                {
                    var text = await ExtractPdfTextAsync(reader);
                    metadata.Abstract = ExtractAbstractFromText(text);
                    
                    // Try to extract DOI from text
                    var doiMatch = Regex.Match(text, @"10\.\d{4,}/[^\s<>""']+", RegexOptions.IgnoreCase);
                    if (doiMatch.Success)
                    {
                        metadata.DOI = doiMatch.Value;
                    }
                }
                catch
                {
                    // If text extraction fails, continue with basic metadata
                }
            }
            catch (Exception ex)
            {
                metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                metadata.Description = $"Error extracting PDF metadata: {ex.Message}";
            }

            return metadata;
        }

        public async Task<Models.DTO.DocumentMetadataDto> ExtractFromWordAsync(string filePath)
        {
            var metadata = new Models.DTO.DocumentMetadataDto
            {
                FilePath = filePath,
                FileType = Path.GetExtension(filePath)
            };

            try
            {
                using var document = WordprocessingDocument.Open(filePath, false);
                var mainPart = document.MainDocumentPart;

                if (mainPart?.DocumentSettingsPart?.Settings != null)
                {
                    var customPropsPart = document.CustomFilePropertiesPart;
                    if (customPropsPart != null)
                    {
                        foreach (var prop in customPropsPart.Properties.Elements<CustomDocumentProperty>())
                        {
                            if (prop.Name == "Title" && prop.VTLPWSTR != null)
                                metadata.Title = prop.VTLPWSTR.Text;
                            else if (prop.Name == "Author" && prop.VTLPWSTR != null)
                                metadata.Author = prop.VTLPWSTR.Text;
                            else if (prop.Name == "Subject" && prop.VTLPWSTR != null)
                                metadata.Subject = prop.VTLPWSTR.Text;
                            else if (prop.Name == "Keywords" && prop.VTLPWSTR != null)
                                metadata.Keywords = prop.VTLPWSTR.Text;
                        }
                    }

                    metadata.Title ??= Path.GetFileNameWithoutExtension(filePath);
                }

                // Get file size
                var fileInfo = new FileInfo(filePath);
                metadata.FileSize = fileInfo.Length;

                // Try to extract text content
                try
                {
                    var text = await ExtractWordTextAsync(mainPart);
                    metadata.Abstract = ExtractAbstractFromText(text);
                }
                catch
                {
                    // If text extraction fails, continue with basic metadata
                }
            }
            catch (Exception ex)
            {
                metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                metadata.Description = $"Error extracting Word metadata: {ex.Message}";
            }

            return metadata;
        }

        public async Task<Models.DTO.DocumentMetadataDto> ExtractFromTextAsync(string filePath)
        {
            var metadata = new Models.DTO.DocumentMetadataDto
            {
                FilePath = filePath,
                FileType = Path.GetExtension(filePath)
            };

            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                metadata.Abstract = ExtractAbstractFromText(content);
                
                // Try to extract basic information from first few lines
                if (lines.Length > 0)
                {
                    metadata.Title = lines[0].Trim();
                }
                
                if (lines.Length > 1)
                {
                    metadata.Author = lines[1].Trim();
                }

                // Get file size
                var fileInfo = new FileInfo(filePath);
                metadata.FileSize = fileInfo.Length;
            }
            catch (Exception ex)
            {
                metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                metadata.Description = $"Error extracting text metadata: {ex.Message}";
            }

            return metadata;
        }

        private async Task<string> ExtractPdfTextAsync(PdfReader reader)
        {
            var text = "";
            for (int i = 1; i <= reader.NumberOfPages; i++)
            {
                text += iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, i);
            }
            return text;
        }

        private async Task<string> ExtractWordTextAsync(MainDocumentPart mainPart)
        {
            if (mainPart?.Document?.Body == null)
                return "";

            var text = "";
            foreach (var paragraph in mainPart.Document.Body.Elements<Paragraph>())
            {
                text += paragraph.InnerText + "\n";
            }
            return text;
        }

        private string ExtractAbstractFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            // Look for abstract section
            var abstractMatch = Regex.Match(text, @"(?i)abstract[:\s]*(.+?)(?=\n\s*\n|\n\s*[A-Z][a-z]+:)", RegexOptions.Singleline);
            if (abstractMatch.Success)
            {
                return abstractMatch.Groups[1].Value.Trim();
            }

            // If no abstract found, return first 500 characters
            return text.Length > 500 ? text.Substring(0, 500) + "..." : text;
        }
    }
}
