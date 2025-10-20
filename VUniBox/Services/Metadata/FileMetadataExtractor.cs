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
    /// <summary>
    /// Service for extracting metadata from various file types.
    /// </summary>
    public class FileMetadataExtractor : IFileMetadataExtractor
    {
        /// <summary>
        /// Extracts metadata from a file based on its extension.
        /// </summary>
        /// <param name="filePath">The full path to the file.</param>
        /// <param name="fileName">The name of the file (including extension).</param>
        /// <returns>A <see cref="Models.DTO.DocumentMetadataDto"/> containing the extracted metadata.</returns>
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

        /// <summary>
        /// Extracts metadata specifically from a PDF file.
        /// </summary>
        /// <param name="filePath">The full path to the PDF file.</param>
        /// <returns>A <see cref="Models.DTO.DocumentMetadataDto"/> containing the extracted metadata from the PDF.</returns>
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

                Console.WriteLine($"[FileMetadataExtractor] Extracting from PDF: {filePath}");
                
                // Extract basic metadata from PDF properties - don't use filename as title fallback
                var pdfTitle = GetPdfProperty(info, "Title");
                Console.WriteLine($"[FileMetadataExtractor] PDF Title property: '{pdfTitle}'");
                
                if (!string.IsNullOrEmpty(pdfTitle) && IsValidTitle(pdfTitle))
                {
                    metadata.Title = CleanTitleText(pdfTitle);
                    Console.WriteLine($"[FileMetadataExtractor] Set title from PDF properties: '{metadata.Title}'");
                }
                metadata.Author = GetPdfProperty(info, "Author") ?? "";
                Console.WriteLine($"[FileMetadataExtractor] PDF Author property: '{metadata.Author}'");
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
                    Console.WriteLine($"[FileMetadataExtractor] Extracted text length: {fullText?.Length ?? 0}");
                    
                    if (!string.IsNullOrEmpty(fullText))
                    {
                        // Fix encoding before extracting metadata
                        fullText = FixVietnameseEncoding(fullText);
                        
                        Console.WriteLine($"[FileMetadataExtractor] Before text extraction - Title: '{metadata.Title}', Publisher: '{metadata.Publisher}'");
                        
                        // Extract metadata from text content
                        ExtractMetadataFromText(metadata, fullText);
                        
                        Console.WriteLine($"[FileMetadataExtractor] After text extraction - Title: '{metadata.Title}', Publisher: '{metadata.Publisher}'");
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
                
                metadata.Description = $"Error extracting PDF metadata: {ex.Message}";
            }

            return metadata;
        }

        /// <summary>
        /// Retrieves a specific property from PDF document information, with Vietnamese encoding fixes.
        /// </summary>
        /// <param name="info">The dictionary containing PDF document information.</param>
        /// <param name="key">The key of the property to retrieve (e.g., "Title", "Author").</param>
        /// <returns>The property value with encoding fixes, or null if the property is not found or empty.</returns>
        private string? GetPdfProperty(Dictionary<string, string> info, string key)
        {
            if (info.ContainsKey(key) && !string.IsNullOrWhiteSpace(info[key]))
            {
                var value = info[key].Trim();
                
                // Fix encoding issues in PDF properties
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        // Common Vietnamese encoding fixes
                        value = value
                            .Replace("Õ", "ọ")
                            .Replace("¸", "á")
                            .Replace("®", "đ")
                            .Replace("häc", "học")
                            .Replace("gi¸o", "giáo")
                            .Replace("dôc", "dục")
                            .Replace("hiÖn", "hiện")
                            .Replace("¹i", "ại")
                            .Replace("¶", "ả")
                            .Replace("ç", "ề")
                            .Replace("Ç", "Ề")
                            .Replace("ß", "ứ")
                            .Replace("Æ", "Ă");
                            
                        // Try to fix encoding by re-encoding
                        var bytes = System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(value);
                        var fixedValue = System.Text.Encoding.UTF8.GetString(bytes);
                        
                        // Only use fixed value if it looks better (contains more Vietnamese chars)
                        if (fixedValue.Contains("ọ") || fixedValue.Contains("á") || fixedValue.Contains("đ") || 
                            fixedValue.Contains("ả") || fixedValue.Contains("ề") || fixedValue.Contains("ứ"))
                        {
                            return fixedValue;
                        }
                    }
                    catch
                    {
                        // If encoding fix fails, return original
                    }
                }
                
                return value;
            }
            return null;
        }

        /// <summary>
        /// Extracts additional metadata from the text content of a document.
        /// </summary>
        /// <param name="metadata">The <see cref="Models.DTO.DocumentMetadataDto"/> object to populate with extracted data.</param>
        /// <param name="text">The full text content of the document.</param>
        private void ExtractMetadataFromText(Models.DTO.DocumentMetadataDto metadata, string text)
        {
            // Clean text first for better parsing
            var cleanText = text.Replace("\n", " ").Replace("\r", " ");
            cleanText = Regex.Replace(cleanText, @"\s+", " ");

            // Extract DOI with more precise patterns
            var doiPatterns = new[]
            {
                @"DOI:\s*(10\.\d{4,}/[^\s<>""'\]\)]+)",
                @"doi:\s*(10\.\d{4,}/[^\s<>""'\]\)]+)",
                @"https?://doi\.org/(10\.\d{4,}/[^\s<>""'\]\)]+)",
                @"\b(10\.\d{4,}/[^\s<>""'\]\)]{6,})\b"
            };

            foreach (var pattern in doiPatterns)
            {
                var doiMatch = Regex.Match(cleanText, pattern, RegexOptions.IgnoreCase);
                if (doiMatch.Success && string.IsNullOrEmpty(metadata.DOI))
                {
                    metadata.DOI = doiMatch.Groups[1].Value.Trim();
                    break;
                }
            
        }

            // Extract academic journal names with improved patterns
            if (string.IsNullOrEmpty(metadata.Journal))
            {
                var journalPatterns = new[]
                {
                    @"Journal of ([^,.\n]+)",
                    @"([^,.\n]*Journal[^,.\n]*?)(?:,|\.|$)",
                    @"((?:American|European|International|British)\s+[^,.\n]*(?:Journal|Review|Quarterly|Studies)[^,.\n]*?)(?:,|\.|$)",
                    @"([A-Z][^,.\n]*(?:Psychology|Education|Science|Research|Studies)[^,.\n]*?)(?:,|\.|$)"
                };

                foreach (var pattern in journalPatterns)
                {
                    var match = Regex.Match(cleanText, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var journal = match.Groups[1].Value.Trim();
                        if (journal.Length > 5 && journal.Length < 100 && !journal.Contains("Copyright"))
                        {
                            metadata.Journal = journal;
                            break;
                        }
                    }
                }
            }

            // Extract volume with better accuracy
            if (string.IsNullOrEmpty(metadata.Volume))
            {
                var volumePatterns = new[]
                {
                    @"(?:Vol\.?\s*|Volume\s+)(\d+)",
                    @"\b(\d{1,3})\s*,\s*\d{1,4}(?:-\d{1,4})?\s*$", // Pattern: "95, 179-187"
                    @"Volume\s+(\d+)\s+Number",
                    @",\s*(\d{1,3})\s*,\s*\d"
                };

                foreach (var pattern in volumePatterns)
                {
                    var match = Regex.Match(cleanText, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var volume = match.Groups[1].Value;
                        if (int.TryParse(volume, out var vol) && vol > 0 && vol < 1000)
                        {
                            metadata.Volume = volume;
                            break;
                        }
                    }
                }
            }

            // Extract issue number with improved patterns
            if (string.IsNullOrEmpty(metadata.Issue))
            {
                var issuePatterns = new[]
                {
                    @"(?:No\.?\s*|Issue\s+|Number\s+)(\d+)",
                    @"Volume\s+\d+\s+Number\s+(\d+)",
                    @"\(\d+\)\s*,\s*(\d+)",
                    @",\s*No\.\s*(\d+)"
                };

                foreach (var pattern in issuePatterns)
                {
                    var match = Regex.Match(cleanText, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var issue = match.Groups[1].Value;
                        if (int.TryParse(issue, out var iss) && iss > 0 && iss < 100)
                        {
                            metadata.Issue = issue;
                            break;
                        }
                    }
                }
            }

            // Extract pages with enhanced patterns
            if (string.IsNullOrEmpty(metadata.Pages))
            {
                var pagePatterns = new[]
                {
                    @"(?:pp\.?\s*|pages?\s*)(\d+)(?:\s*[-–]\s*(\d+))?",
                    @"\b(\d{1,4})\s*[-–]\s*(\d{1,4})\b",
                    @",\s*(\d{2,4})(?:\s*[-–]\s*(\d{2,4}))?\s*$",
                    @"pages?\s+(\d+)(?:\s*[-–]\s*(\d+))?"
                };

                foreach (var pattern in pagePatterns)
                {
                    var match = Regex.Match(cleanText, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var startPage = match.Groups[1].Value;
                        var endPage = match.Groups[2].Success ? match.Groups[2].Value : "";

                        if (int.TryParse(startPage, out var start) && start > 0)
                        {
                            if (!string.IsNullOrEmpty(endPage) && int.TryParse(endPage, out var end) && end > start)
                            {
                                metadata.Pages = $"{startPage}-{endPage}";
                            }
                            else
                            {
                                metadata.Pages = startPage;
                            }
                            break;
                        }
                    }
                }
            }

            // Extract publication year with better accuracy
            if (!metadata.PublicationDate.HasValue)
            {
                var yearPatterns = new[]
                 {
                    @"Copyright\s+(19|20)\d{2}",
                    @"\b(19|20)\d{2}\b(?:\s+by)",
                    @"(?:Published|Copyright)\s+(?:in\s+)?(19|20)\d{2}",
                    @"\b(19|20)\d{2}\b"
                };

                foreach (var pattern in yearPatterns)
                {
                    var yearMatch = Regex.Match(cleanText, pattern, RegexOptions.IgnoreCase);
                    if (yearMatch.Success && int.TryParse(yearMatch.Value.Substring(yearMatch.Value.Length - 4), out var year))
                    {
                        if (year >= 1900 && year <= DateTime.Now.Year)
                        {
                            metadata.PublicationDate = new DateOnly(year, 1, 1);
                            break;
                        }
                    }
                }
            }

                // Extract publisher with enhanced patterns
                if (string.IsNullOrEmpty(metadata.Publisher))
                {
                    var publisherPatterns = new[]
                    {
                    @"Copyright\s+\d{4}\s+by\s+(.+?)(?:\.|,|$)",
                    @"(?:Published by|Publisher:)\s*([^.\n]+)",
                    @"(American Psychological Association)",
                    @"(Springer|Elsevier|IEEE|ACM|Nature|Science|Wiley|Cambridge|Oxford)[^.\n]*",
                    @"([A-Z][^.]*Press[^.]*)",
                    @"([A-Z][^.]*Publications?[^.]*)"
                };

                    foreach (var pattern in publisherPatterns)
                    {
                        var match = Regex.Match(cleanText, pattern, RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var publisher = match.Groups[1].Value.Trim();
                            if (publisher.Length > 3 && publisher.Length < 100)
                            {
                                metadata.Publisher = publisher;
                                break;
                            }
                        }
                    }
                }

                // Extract authors with improved accuracy
                if (string.IsNullOrEmpty(metadata.Authors))
                {
                    var authorPatterns = new[]
                    {
                     @"([A-Z][a-z]+\s+[A-Z]\.\s+[A-Z][a-z]+)", // Christopher A. Wolters
                    @"([A-Z][a-z]+,\s+[A-Z]\.\s*[A-Z]\.)", // Smith, J. A.
                    @"(?:Authors?:?\s*)([A-Z][^.\n]+(?:,\s*[A-Z][^.\n]+)*)",
                     @"(?:By:?\s*)([A-Z][^.\n]+(?:,\s*[A-Z][^.\n]+)*)",
                    @"^([A-Z][a-z]+\s+[A-Z][a-z]+)" // First line author
                };

                    foreach (var pattern in authorPatterns)
                    {
                        var match = Regex.Match(cleanText, pattern, RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var authors = match.Groups[1].Value.Trim();
                            if (authors.Length > 3 && authors.Length < 200 && !authors.Contains("Copyright"))
                            {
                                metadata.Authors = authors;
                                if (string.IsNullOrEmpty(metadata.Author))
                                {
                                    metadata.Author = authors.Split(',')[0].Trim();
                                }
                                break;
                            }
                        }
                    }
                }

                // Extract title if not already set
                if (string.IsNullOrEmpty(metadata.Title) || metadata.Title.Contains("temp"))
                {
                    var titlePatterns = new[]
                    {
                    // Academic paper title patterns - most specific first
                    @"(?i)(?:title[:\s]*)?([A-Z][^.\n\r]{20,200}?)(?=\s*\n\s*[A-Z][a-z]+\s+[A-Z]\.?\s*[A-Z][a-z]+)",
                    @"(?i)([A-Z][^.\n\r]{15,150}?)(?=\s*\n\s*(?:Abstract|Tóm tắt|Introduction|ABSTRACT))",
                    @"^([A-Z][A-Za-z\s\-:,]{20,200}?)(?=\n\s*[A-Z][a-z]+\s+[A-Z]\.)",
                    // Look for title before author patterns
                    @"([A-Z][A-Za-z\s\-:,?]{15,200}?)(?=\s*\n\s*(?:[A-Z][a-z]+\s+[A-Z]\.\s*[A-Z][a-z]+|Department|University))",
                    // Vietnamese title patterns
                    @"(?i)(?:tiêu đề[:\s]*)?([A-ZÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮỲÝỴỶỸĐ][^.\n\r]{15,200}?)(?=\s*\n)",
                    // More specific academic patterns
                    @"^(?!Copyright|Journal|Copyright\s+\d{4})([A-Z][A-Za-z\s\-:,?]{20,200}?)(?=\s*\n(?:[A-Z][a-z]+\s+[A-Z]\.|Abstract|Introduction))",
                    // Study-related patterns
                    @"Understanding\s+([^.\n]+)", // "Understanding Procrastination"
                    @"([A-Z][^.\n]*(?:Perspective|Analysis|Study|Research)[^.\n]*)",
                    // Last resort - first meaningful line
                    @"^(?!Copyright|Journal|Page|\d+)([A-Z][A-Za-z\s\-:,]{10,150}?)(?=\n|$)"
                };

                    foreach (var pattern in titlePatterns)
                    {
                    var titleMatch = Regex.Match(cleanText, pattern, RegexOptions.Multiline);
                    if (titleMatch.Success)
                    {
                        var title = titleMatch.Groups[1].Value.Trim();

                        // Clean and validate title
                        title = CleanTitleText(title);

                        if (IsValidTitle(title))
                        {
                            metadata.Title = CleanAndLimitText(title, 200);
                            break;
                        }
                    }
                }

                // If still no title found, try to extract from first few meaningful lines
                if (string.IsNullOrEmpty(metadata.Title))
                {
                    var lines = cleanText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines.Take(10))
                    {
                        var cleanLine = line.Trim();
                        if (cleanLine.Length > 15 && cleanLine.Length < 200 &&
                            !IsHeaderOrPageNumber(cleanLine) &&
                            !cleanLine.StartsWith("Copyright", StringComparison.OrdinalIgnoreCase) &&
                            !cleanLine.Contains("Journal of") &&
                            char.IsUpper(cleanLine[0]))
                        {
                            metadata.Title = CleanAndLimitText(cleanLine, 200);
                            break;
                        }
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
                    metadata.Language = DetectLanguage(cleanText);
                }
            
        }


        /// <summary>
        /// Cleans title text by removing unwanted patterns
        /// </summary>
        /// <param name="title">Raw title text</param>
        /// <returns>Cleaned title text</returns>
        private string CleanTitleText(string title)
        {
            if (string.IsNullOrEmpty(title))
                return title;

            // Remove common unwanted patterns
            title = Regex.Replace(title, @"Copyright\s+\d{4}.*?by.*?\.", "", RegexOptions.IgnoreCase);
            title = Regex.Replace(title, @"Journal\s+of\s+.*?Psychology", "", RegexOptions.IgnoreCase);
            title = Regex.Replace(title, @"\d{4}-\d{4}", ""); // Remove ISSN
            title = Regex.Replace(title, @"\$\d+\.\d+", ""); // Remove price
            title = Regex.Replace(title, @"^\d+\s*", ""); // Remove leading numbers
            title = Regex.Replace(title, @"Vol\.\s*\d+", "", RegexOptions.IgnoreCase); // Remove volume info
            title = Regex.Replace(title, @"pp\.\s*\d+", "", RegexOptions.IgnoreCase); // Remove page info
            title = Regex.Replace(title, @"\s+", " "); // Normalize whitespace

            return title.Trim();
        }

        /// <summary>
        /// Validates if the extracted text is a good title
        /// </summary>
        /// <param name="title">Title to validate</param>
        /// <returns>True if title appears to be valid</returns>
        private bool IsValidTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Length < 10 || title.Length > 200)
                return false;

            // Should not contain these patterns
            var lowerTitle = title.ToLower();
            if (lowerTitle.Contains("copyright") ||
                lowerTitle.Contains("journal of") ||
                lowerTitle.Contains("0022-0663") ||
                lowerTitle.Contains("$12.") ||
                lowerTitle.StartsWith("page ") ||
                Regex.IsMatch(title, @"^\d+\s*$"))
                return false;

            // Should not be all uppercase (likely header)
            if (title == title.ToUpper() && title.Length < 50)
                return false;

            // Should not be filename-like
            if (title.Contains("temp") || title.Contains(".pdf") || title.Contains(".doc"))
                return false;

            // Should start with capital letter
            if (!char.IsUpper(title[0]))
                return false;

            // Should contain meaningful words (not just numbers/symbols)
            var wordCount = title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            return wordCount >= 3; // At least 3 words for a meaningful title
        }

        /// <summary>
        /// Extracts metadata specifically from a Word (.docx or .doc) file.
        /// </summary>
        /// <param name="filePath">The full path to the Word file.</param>
        /// <returns>A <see cref="Models.DTO.DocumentMetadataDto"/> containing the extracted metadata from the Word document.</returns>
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

                //// Fallback to filename if no title found
                //if (string.IsNullOrEmpty(metadata.Title))
                //{
                //    metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                //}

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
                //metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                metadata.Description = $"Error extracting Word metadata: {ex.Message}";
            }

            return metadata;
        }

        /// <summary>
        /// Extracts metadata specifically from a plain text (.txt or .md) file.
        /// </summary>
        /// <param name="filePath">The full path to the text file.</param>
        /// <returns>A <see cref="Models.DTO.DocumentMetadataDto"/> containing the extracted metadata from the text file.</returns>
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
                
                
                metadata.Abstract = ExtractAbstractFromText(content);
                
                // Try to extract basic information from first few lines
                if (lines.Length > 0)
                {
                    // Try to get title from first meaningful line
                    var firstMeaningfulLine = lines.FirstOrDefault(line =>
                        line.Trim().Length > 10 &&
                        !IsHeaderOrPageNumber(line.Trim()) &&
                        char.IsUpper(line.Trim()[0]));

                    if (!string.IsNullOrEmpty(firstMeaningfulLine) && IsValidTitle(firstMeaningfulLine.Trim()))
                    {
                        metadata.Title = CleanTitleText(firstMeaningfulLine.Trim());
                    }
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
                
                metadata.Description = $"Error extracting text metadata: {ex.Message}";
            }

            return metadata;
        }

        /// <summary>
        /// Extracts all text content from a PDF reader.
        /// </summary>
        /// <param name="reader">The <see cref="PdfReader"/> instance.</param>
        /// <returns>The concatenated text content from all pages of the PDF.</returns>
        private async Task<string> ExtractPdfTextAsync(PdfReader reader)
        {
            var text = "";
            try
            {
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    var pageText = iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, i);
                    
                    // Fix encoding issues by handling UTF-8 properly
                    if (!string.IsNullOrEmpty(pageText))
                    {
                        // Clean up common encoding issues
                        pageText = pageText
                            .Replace("Õ", "ọ")
                            .Replace("¸", "á")
                            .Replace("®", "đ")
                            .Replace("häc", "học")
                            .Replace("gi¸o", "giáo")
                            .Replace("dôc", "dục")
                            .Replace("hiÖn", "hiện")
                            .Replace("¹i", "ại");
                            
                        // Try to fix more encoding issues
                        var bytes = System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(pageText);
                        pageText = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    
                    text += pageText;
                }
            }
            catch (Exception)
            {
                // If encoding fix fails, fallback to original extraction
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    text += iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, i);
                }
            }
            return text;
        }

        /// <summary>
        /// Extracts all text content from the main document part of a WordprocessingDocument.
        /// </summary>
        /// <param name="mainPart">The main document part of the WordprocessingDocument.</param>
        /// <returns>The concatenated text content from all paragraphs in the Word document.</returns>
        private async Task<string> ExtractWordTextAsync(MainDocumentPart mainPart)
        {
            if (mainPart?.Document?.Body == null)
                return "";

            var text = "";
            try
            {
                foreach (var paragraph in mainPart.Document.Body.Elements<Paragraph>())
                {
                    var paragraphText = paragraph.InnerText;
                    
                    // Fix encoding issues in Word text
                    if (!string.IsNullOrEmpty(paragraphText))
                    {
                        paragraphText = paragraphText
                            .Replace("Õ", "ọ")
                            .Replace("¸", "á")
                            .Replace("®", "đ")
                            .Replace("häc", "học")
                            .Replace("gi¸o", "giáo")
                            .Replace("dôc", "dục")
                            .Replace("hiÖn", "hiện")
                            .Replace("¹i", "ại")
                            .Replace("¶", "ả")
                            .Replace("ç", "ề")
                            .Replace("ß", "ứ");
                    }
                    
                    text += paragraphText + "\n";
                }
            }
            catch (Exception)
            {
                // Fallback to simple extraction
                foreach (var paragraph in mainPart.Document.Body.Elements<Paragraph>())
                {
                    text += paragraph.InnerText + "\n";
                }
            }
            
            return text;
        }

        /// <summary>
        /// Extracts an abstract or a summary from the given text content.
        /// Prioritizes sections explicitly marked as "abstract" or similar.
        /// </summary>
        /// <param name="text">The full text content of the document.</param>
        /// <returns>The extracted abstract or a meaningful summary, cleaned and limited in length.</returns>
        private string ExtractAbstractFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            // Fix encoding issues first
            text = FixVietnameseEncoding(text);
            
            // Clean up extra whitespace and line breaks
            text = Regex.Replace(text, @"\s+", " ").Trim();

            // Look for abstract section in multiple languages
            var abstractPatterns = new[]
            {
                @"(?i)abstract[:\s]*(.{50,500}?)(?=\n\s*\n|\n\s*[A-Z][a-z]+:|keywords|introduction|1\.|$)",
                @"(?i)tóm\s*tắt[:\s]*(.{50,500}?)(?=\n\s*\n|\n\s*[A-Z]|$)",
                @"(?i)tổng\s*quan[:\s]*(.{50,500}?)(?=\n\s*\n|\n\s*[A-Z]|$)",
                @"(?i)giới\s*thiệu[:\s]*(.{50,500}?)(?=\n\s*\n|\n\s*[A-Z]|$)",
                // Look for content after copyright but before first section
                @"Copyright\s+\d{4}.*?by.*?\.\s*(.{100,500}?)(?=\n\s*[A-Z][a-z]+:|1\.|Introduction|$)",
                // Look for meaningful first paragraph after title
                @"([A-Z][a-z]+.*?(?:research|study|analysis|investigation|understanding).*?\.)"
            };

            foreach (var pattern in abstractPatterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var abstractText = match.Groups[1].Value.Trim();

                    // Clean up the abstract
                    abstractText = CleanAbstractText(abstractText);

                    // Validate abstract quality
                    if (IsValidAbstract(abstractText))
                    {
                        return CleanAndLimitText(abstractText, 400);
                    }
                }
            }

            // Look for introduction or first meaningful paragraph
            var sentences = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var meaningfulText = "";
            
            foreach (var sentence in sentences)
            {
                var cleanSentence = sentence.Trim();
                if (cleanSentence.Length > 20 && !IsHeaderOrPageNumber(cleanSentence) &&
                   !cleanSentence.StartsWith("Copyright") && !cleanSentence.Contains("0022-0663"))
                {
                    
                    meaningfulText += cleanSentence + ". ";
                        if (meaningfulText.Length > 250)
                            break;
                }
            }

                return CleanAndLimitText(meaningfulText, 400);
            }

        /// <summary>
        /// Cleans abstract text by removing unwanted patterns
        /// </summary>
        /// <param name="text">Raw abstract text</param>
        /// <returns>Cleaned abstract text</returns>
        private string CleanAbstractText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove common unwanted patterns
            text = Regex.Replace(text, @"Copyright\s+\d{4}.*?by.*?\.", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\d{4}-\d{4}", ""); // Remove ISSN
            text = Regex.Replace(text, @"\$\d+\.\d+", ""); // Remove price
            text = Regex.Replace(text, @"^\d+\s*", ""); // Remove leading numbers
            text = Regex.Replace(text, @"\s+", " "); // Normalize whitespace

            return text.Trim();
        }

        /// <summary>
        /// Validates if the extracted text is a good abstract
        /// </summary>
        /// <param name="text">Text to validate</param>
        /// <returns>True if text appears to be a valid abstract</returns>
        private bool IsValidAbstract(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 50)
                return false;

            // Check for abstract-like characteristics
            var lowerText = text.ToLower();

            // Should not contain these patterns
            if (lowerText.Contains("copyright") ||
                lowerText.Contains("0022-0663") ||
                lowerText.Contains("$12.") ||
                Regex.IsMatch(text, @"^\d+\s*$"))
                return false;

            // Should contain meaningful academic words
            var academicWords = new[] { "study", "research", "analysis", "understanding", "investigate",
                                      "examine", "approach", "method", "result", "finding", "student",
                                      "learning", "perspective", "theory", "model" };

            var academicWordCount = academicWords.Count(word => lowerText.Contains(word));
            return academicWordCount >= 2;
        }

        /// <summary>
        /// Cleans and limits the length of a given text string.
        /// Removes unwanted characters and ensures the text does not exceed a specified maximum length.
        /// </summary>
        /// <param name="text">The text string to clean and limit.</param>
        /// <param name="maxLength">The maximum desired length of the text.</param>
        /// <returns>The cleaned and length-limited text.</returns>
        private string CleanAndLimitText(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            // Remove unwanted characters and patterns
            text = Regex.Replace(text, @"[|\\\/\{\}\[\]<>]", " ");
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();

            // Limit length and ensure proper ending
            if (text.Length > maxLength)
            {
                var cutPoint = text.LastIndexOf(' ', maxLength - 3);
                if (cutPoint > maxLength / 2)
                {
                    text = text.Substring(0, cutPoint) + "...";
                }
                else
                {
                    text = text.Substring(0, maxLength - 3) + "...";
                }
            }

            return text;
        }

        /// <summary>
        /// Checks if a given text string appears to be a header or a page number.
        /// </summary>
        /// <param name="text">The text string to check.</param>
        /// <returns>True if the text is likely a header or page number, false otherwise.</returns>
        private bool IsHeaderOrPageNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            text = text.Trim();

            // Page numbers (various formats)
            if (Regex.IsMatch(text, @"^\d+$") ||                          // Simple page numbers
                Regex.IsMatch(text, @"^Page\s*\d+", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(text, @"^\d+\s*of\s*\d+", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(text, @"^\[\d+\]$") ||                       // [1], [2], etc.
                Regex.IsMatch(text, @"^-\s*\d+\s*-$"))                     // - 1 -, - 2 -, etc.
                return true;

            // Academic paper headers/footers
            if (Regex.IsMatch(text, @"Journal\s+of\s+Educational\s+Psychology", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(text, @"Copyright\s+\d{4}", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(text, @"American\s+Psychological\s+Association", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(text, @"DOI:\s*10\.\d+", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(text, @"ISSN\s*\d{4}-\d{4}", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(text, @"Vol\.\s*\d+", RegexOptions.IgnoreCase) ||
                text.Contains("0022-0663"))
                return true;

            // Publication identifiers
            if (Regex.IsMatch(text, @"^\d{4}-\d{4}") ||                    // ISSN numbers
                Regex.IsMatch(text, @"^10\.\d+/") ||                       // DOI numbers
                Regex.IsMatch(text, @"\$\d+\.\d+"))                        // Price information
                return true;

            // Headers/Footers patterns
            if (text.StartsWith("© ") ||                                   // Copyright symbols
                text.All(char.IsUpper) && text.Length < 50 ||             // ALL CAPS short lines
                Regex.IsMatch(text, @"^[A-Z\s]{3,30}$") ||                // Short uppercase text
                Regex.IsMatch(text, @"^\d+\s+[A-Z][A-Z\s]+$"))            // Number followed by caps
                return true;

            // Very short lines that are likely metadata
            if (text.Length < 5 ||
                (text.Length < 10 && !text.Contains(" ")))
                return true;

            // Lines with only special characters or numbers
            if (Regex.IsMatch(text, @"^[\d\s\-_=\.]+$"))
                return true;

            // Chapter/section headers
            if (Regex.IsMatch(text, @"^(chương|chapter|phần|part)\s*\d+", RegexOptions.IgnoreCase))
                return true;

            // Running headers (repeated text at top/bottom of pages)
            var commonHeaders = new[] { "INTRODUCTION", "METHODOLOGY", "RESULTS", "DISCUSSION",
                                       "CONCLUSION", "REFERENCES", "APPENDIX", "ABSTRACT" };
            if (commonHeaders.Any(header => text.Equals(header, StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        /// <summary>
        /// Attempts to fix common Vietnamese encoding issues in a given text string.
        /// This method addresses specific incorrect character mappings found in some documents.
        /// </summary>
        /// <param name="text">The text string to fix encoding for.</param>
        /// <returns>The text string with common Vietnamese encoding issues resolved.</returns>
        private string FixVietnameseEncoding(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            try
            {
                // Fix common Vietnamese encoding issues from your specific text
                text = text
                    // Remove garbage characters first
                    .Replace("l OM oAR cP SD", "")
                    .Replace("47 20 6 07 1", "")
                    
                    // Major pattern fixes for capital letters
                    .Replace("GIO", "GIÁO")
                    .Replace("TRNH", "TRÌNH") 
                    .Replace("TU?NG", "TƯỞNG")
                    .Replace("H?", "HỒ")
                    .Replace("CH", "CHÍ")
                    .Replace("KHI", "KHÁI")
                    .Replace("NI?M", "NIỆM")
                    .Replace("D?I", "ĐỐI")
                    .Replace("PHP", "PHÁP")
                    .Replace("NGHIN", "NGHIÊN")
                    .Replace("C?U", "CỨU")
                    .Replace("NGHIA", "NGHĨA")
                    .Replace("H?C", "HỌC")
                    .Replace("T?P", "TẬP")
                    .Replace("MN", "MÔN")
                    .Replace("N?I", "NỘI")
                    .Replace("CHUONG", "CHƯƠNG")
                    
                    // Lowercase fixes
                    .Replace("gio", "giáo")
                    .Replace("trnh", "trình")
                    .Replace("tu?ng", "tưởng")
                    .Replace("h?", "hồ")
                    .Replace("ch", "chí")
                    .Replace("khi", "khái")
                    .Replace("ni?m", "niệm")
                    .Replace("d?i", "đối")
                    .Replace("php", "pháp")
                    .Replace("nghin", "nghiên")
                    .Replace("c?u", "cứu")
                    .Replace("nghia", "nghĩa")
                    .Replace("h?c", "học")
                    .Replace("t?p", "tập")
                    .Replace("mn", "môn")
                    .Replace("n?i", "nội")
                    .Replace("chuong", "chương")
                    
                    // Two character combinations
                    .Replace("?i", "ại")
                    .Replace("?u", "ấu")
                    .Replace("?a", "ưa")
                    .Replace("?n", "ần")
                    .Replace("?c", "ức")
                    .Replace("?t", "ột")
                    .Replace("?p", "ập")
                    .Replace("?m", "ầm")
                    .Replace("?g", "ững")
                    .Replace("?r", "ưr")
                    .Replace("?s", "ưs")
                    .Replace("?l", "ưl")
                    .Replace("?k", "ưk")
                    .Replace("?d", "ướd")
                    .Replace("?f", "ướf")
                    .Replace("?v", "ướv")
                    .Replace("?w", "ướw")
                    .Replace("?x", "ướx")
                    .Replace("?y", "ướy")
                    .Replace("?z", "ướz")
                    
                    // Common word patterns
                    .Replace("bi?u", "biểu")
                    .Replace("ton", "toàn")
                    .Replace("qu?c", "quốc")
                    .Replace("l?n", "lần")
                    .Replace("th?", "thế")
                    .Replace("c?a", "của")
                    .Replace("D?ng", "Đảng")
                    .Replace("C?ng", "Cộng")
                    .Replace("s?n", "sản")
                    .Replace("Vi?t", "Việt")
                    .Replace("nu", "nêu")
                    .Replace("như", "như")
                    
                    // Single character replacements
                    .Replace("", "ô")
                    .Replace("?", "ệ")
                    .Replace("Õ", "ọ")
                    .Replace("¸", "á") 
                    .Replace("®", "đ")
                    .Replace("ç", "ề")
                    .Replace("Ç", "Ề")
                    .Replace("ß", "ứ")
                    .Replace("Æ", "Ă")
                    
                    // Additional patterns from the new text
                    .Replace("GIôO", "GIÁO")
                    .Replace("TRôNH", "TRÌNH")
                    .Replace("TUệNG", "TƯỞNG")
                    .Replace("Hệ", "HỒ")
                    .Replace("CHô", "CHÍ")
                    .Replace("KHôI", "KHÁI")
                    .Replace("NIệM", "NIỆM")
                    .Replace("Dệi", "ĐỐI")
                    .Replace("PHôP", "PHÁP")
                    .Replace("NGHIôN", "NGHIÊN")
                    .Replace("Cệu", "CỨU")
                    .Replace("NGHĩA", "NGHĨA")
                    .Replace("Hệc", "HỌC")
                    .Replace("Tệp", "TẬP")
                    .Replace("Môn", "MÔN")
                    .Replace("Nệi", "NỘI");

                // Remove remaining garbage patterns
                text = System.Text.RegularExpressions.Regex.Replace(text, @"[|\\\/\{\}\[\]<>★☆■□●○]", " ");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                text = text.Trim();

                return text;
            }
            catch (Exception)
            {
                return text;
            }
        }
        /// <summary>
        /// Detects the language of the given text based on common language patterns
        /// </summary>
        /// <param name="text">The text to analyze for language detection</param>
        /// <returns>Language code (en, vi, etc.)</returns>
        private string DetectLanguage(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "en";

            // Check for Vietnamese characters
            if (Regex.IsMatch(text, @"[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđĐ]"))
                return "vi";

            // Check for common Vietnamese words
            var vietnameseWords = new[] { "và", "của", "trong", "một", "với", "này", "những", "được", "có", "cho", "từ", "theo", "về", "sẽ", "các", "người", "giáo", "học", "nghiên", "cứu" };
            var lowerText = text.ToLower();
            var vietnameseWordCount = vietnameseWords.Count(word => lowerText.Contains(word));

            if (vietnameseWordCount >= 3)
                return "vi";

            // Default to English for academic papers
            return "en";
        }
    }
}
