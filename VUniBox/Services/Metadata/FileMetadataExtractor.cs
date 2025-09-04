using iTextSharp.text.pdf;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.ExtendedProperties;
using System.Text.RegularExpressions;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.DTO;
using DocumentFormat.OpenXml.CustomProperties;
using System.Xml.Linq;

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
                FileType = ".pdf",
                RetrievedDate = DateTime.UtcNow
            };

            try
            {
                using var reader = new PdfReader(filePath);
                var info = reader.Info;

                // Extract basic metadata from PDF properties
                metadata.Title = GetPdfProperty(info, "Title") ?? Path.GetFileNameWithoutExtension(filePath);
                metadata.Author = GetPdfProperty(info, "Author") ?? "";
                metadata.Subject = GetPdfProperty(info, "Subject") ?? "";
                metadata.Keywords = GetPdfProperty(info, "Keywords") ?? "";

                // Extract creation date
                var creationDate = GetPdfProperty(info, "CreationDate");
                if (!string.IsNullOrEmpty(creationDate))
                {
                    // PDF dates are in format "D:YYYYMMDDHHmmSSOHH'mm'"
                    var cleanDate = Regex.Replace(creationDate, @"D:(\d{8})", "$1");
                    if (DateTime.TryParseExact(cleanDate.Substring(0, Math.Min(8, cleanDate.Length)), 
                                             "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
                    {
                        metadata.PublicationDate = DateOnly.FromDateTime(date);
                    }
                }

                // Extract creator/producer as publisher fallback
                var creator = GetPdfProperty(info, "Creator") ?? "";
                var producer = GetPdfProperty(info, "Producer") ?? "";
                if (!string.IsNullOrEmpty(creator) && !creator.Contains("Microsoft") && !creator.Contains("Adobe"))
                {
                    metadata.Publisher = creator;
                }

                // Get file size
                var fileInfo = new FileInfo(filePath);
                metadata.FileSize = fileInfo.Length;

                // Extract text content for additional metadata mining
                try
                {
                    var fullText = await ExtractPdfTextAsync(reader);
                    
                    if (!string.IsNullOrEmpty(fullText))
                    {
                        // Extract metadata from text content
                        ExtractMetadataFromText(metadata, fullText);
                    }
                }
                catch (Exception ex)
                {
                    // Log but continue with basic metadata
                    metadata.Description = $"Text extraction failed: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                metadata.Description = $"Error extracting PDF metadata: {ex.Message}";
            }

            return metadata;
        }

        private string? GetPdfProperty(Dictionary<string, string> info, string key)
        {
            if (info.ContainsKey(key) && !string.IsNullOrWhiteSpace(info[key]))
            {
                return info[key].Trim();
            }
            return null;
        }

        private void ExtractMetadataFromText(Models.DTO.DocumentMetadataDto metadata, string text)
        {
            // Extract DOI
            var doiMatch = Regex.Match(text, @"(?:DOI:?\s*)?10\.\d{4,}/[^\s<>""'\]\)]+", RegexOptions.IgnoreCase);
            if (doiMatch.Success)
            {
                metadata.DOI = doiMatch.Value.Replace("DOI:", "").Trim();
            }

            // Extract journal name (look for patterns like "Journal of...", "IEEE...", etc.)
            var journalPatterns = new[]
            {
                @"(?:published in|appears in|from)\s+([A-Z][^.]+(?:Journal|Transactions|Proceedings|Review|Letters)[^.]*)",
                @"(IEEE\s+[^.]+)",
                @"(ACM\s+[^.]+)",
                @"([A-Z][^.]*Journal[^.]*)",
                @"([A-Z][^.]*Proceedings[^.]*)"
            };

            foreach (var pattern in journalPatterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success && string.IsNullOrEmpty(metadata.Journal))
                {
                    metadata.Journal = match.Groups[1].Value.Trim();
                    break;
                }
            }

            // Extract volume, issue, pages
            var volumeMatch = Regex.Match(text, @"(?:Vol\.?\s*|Volume\s+)(\d+)", RegexOptions.IgnoreCase);
            if (volumeMatch.Success)
            {
                metadata.Volume = volumeMatch.Groups[1].Value;
            }

            var issueMatch = Regex.Match(text, @"(?:No\.?\s*|Issue\s+|Number\s+)(\d+)", RegexOptions.IgnoreCase);
            if (issueMatch.Success)
            {
                metadata.Issue = issueMatch.Groups[1].Value;
            }

            var pagesMatch = Regex.Match(text, @"(?:pp\.?\s*|pages?\s*)(\d+)(?:\s*[-–—]\s*(\d+))?", RegexOptions.IgnoreCase);
            if (pagesMatch.Success)
            {
                metadata.Pages = pagesMatch.Groups[2].Success 
                    ? $"{pagesMatch.Groups[1].Value}-{pagesMatch.Groups[2].Value}"
                    : pagesMatch.Groups[1].Value;
            }

            // Extract publication year from text if not found in properties
            if (!metadata.PublicationDate.HasValue)
            {
                var yearMatch = Regex.Match(text, @"\b(19|20)\d{2}\b");
                if (yearMatch.Success && int.TryParse(yearMatch.Value, out var year))
                {
                    metadata.PublicationDate = new DateOnly(year, 1, 1);
                }
            }

            // Extract publisher
            if (string.IsNullOrEmpty(metadata.Publisher))
            {
                var publisherPatterns = new[]
                {
                    @"(?:Published by|Publisher:)\s*([^.\n]+)",
                    @"(Springer|Elsevier|IEEE|ACM|Nature|Science|Wiley)[^.\n]*",
                    @"([A-Z][^.]*Press[^.]*)",
                    @"([A-Z][^.]*Publications?[^.]*)"
                };

                foreach (var pattern in publisherPatterns)
                {
                    var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        metadata.Publisher = match.Groups[1].Value.Trim();
                        break;
                    }
                }
            }

            // Extract authors (look for multiple author patterns)
            if (string.IsNullOrEmpty(metadata.Authors))
            {
                var authorPatterns = new[]
                {
                    @"(?:Authors?:?\s*)([A-Z][^.\n]+(?:,\s*[A-Z][^.\n]+)*)",
                    @"(?:By:?\s*)([A-Z][^.\n]+(?:,\s*[A-Z][^.\n]+)*)"
                };

                foreach (var pattern in authorPatterns)
                {
                    var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var authors = match.Groups[1].Value.Trim();
                        metadata.Authors = authors;
                        if (string.IsNullOrEmpty(metadata.Author))
                        {
                            metadata.Author = authors.Split(',')[0].Trim();
                        }
                        break;
                    }
                }
            }

            // Extract abstract
            if (string.IsNullOrEmpty(metadata.Abstract))
            {
                metadata.Abstract = ExtractAbstractFromText(text);
            }

            // Set language
            if (string.IsNullOrEmpty(metadata.Language))
            {
                metadata.Language = "en"; // Default to English for academic papers
            }
        }

        public async Task<Models.DTO.DocumentMetadataDto> ExtractFromWordAsync(string filePath)
        {
            var metadata = new Models.DTO.DocumentMetadataDto
            {
                FilePath = filePath,
                FileType = Path.GetExtension(filePath),
                RetrievedDate = DateTime.UtcNow
            };

            try
            {
                using var document = WordprocessingDocument.Open(filePath, false);
                var mainPart = document.MainDocumentPart;

                // Simple extraction from custom properties
                var customPropsPart = document.CustomFilePropertiesPart;
                if (customPropsPart != null)
                {
                    foreach (var prop in customPropsPart.Properties.Elements<CustomDocumentProperty>())
                    {
                        var name = prop.Name?.Value?.ToLower();
                        var value = prop.VTLPWSTR?.Text;
                        
                        if (!string.IsNullOrEmpty(value))
                        {
                            switch (name)
                            {
                                case "title": metadata.Title = value; break;
                                case "author": metadata.Author = value; break;
                                case "authors": metadata.Authors = value; break;
                                case "journal": metadata.Journal = value; break;
                                case "publisher": metadata.Publisher = value; break;
                                case "doi": metadata.DOI = value; break;
                                case "isbn": metadata.ISBN = value; break;
                                case "volume": metadata.Volume = value; break;
                                case "issue": metadata.Issue = value; break;
                                case "pages": metadata.Pages = value; break;
                                case "keywords": metadata.Keywords = value; break;
                                case "subject": metadata.Subject = value; break;
                            }
                        }
                    }
                }

                // Fallback to filename if no title found
                if (string.IsNullOrEmpty(metadata.Title))
                {
                    metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                }

                // Get file size
                var fileInfo = new FileInfo(filePath);
                metadata.FileSize = fileInfo.Length;

                // Extract text content for additional metadata mining
                if (mainPart != null)
                {
                    try
                    {
                        var text = await ExtractWordTextAsync(mainPart);
                        if (!string.IsNullOrEmpty(text))
                        {
                            ExtractMetadataFromText(metadata, text);
                        }
                    }
                    catch (Exception ex)
                    {
                        metadata.Description = $"Text extraction failed: {ex.Message}";
                    }
                }

                // Set default language
                if (string.IsNullOrEmpty(metadata.Language))
                {
                    metadata.Language = "en";
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
