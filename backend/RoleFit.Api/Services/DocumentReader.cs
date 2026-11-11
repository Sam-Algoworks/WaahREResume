using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace RoleFit.Api.Services;

public sealed class DocumentReader
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private const int MaxCharacters = 150_000;

    public async Task<string> ReadAsync(IFormFile file, CancellationToken cancellation)
    {
        if (file.Length is 0 or > MaxBytes)
            throw new ArgumentException("File must be between 1 byte and 5 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".txt" or ".docx" or ".pdf"))
            throw new ArgumentException("Upload a .pdf, .docx, or .txt document.");

        await using var source = file.OpenReadStream();
        using var memory = new MemoryStream();
        await source.CopyToAsync(memory, cancellation);
        var bytes = memory.ToArray();

        string text;
        switch (ext)
        {
            case ".txt":
                text = Encoding.UTF8.GetString(bytes);
                break;
            case ".docx":
                if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B)
                    throw new ArgumentException("The file is not a valid DOCX document.");
                text = ExtractDocx(bytes);
                break;
            default:
                if (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-")
                    throw new ArgumentException("The file is not a valid PDF document.");
                text = ExtractPdf(bytes);
                break;
        }
        text = text.Replace("\0", " ").Trim();
        if (text.Length > MaxCharacters)
            text = text[..MaxCharacters];
        if (text.Length < 30)
            throw new ArgumentException("Not enough selectable text was found. Scanned PDFs need OCR, which is planned for a later phase.");
        return text;
    }

    private static string ExtractPdf(byte[] bytes)
    {
        using var document = PdfDocument.Open(bytes);
        if (document.NumberOfPages > 40)
            throw new ArgumentException("Only documents up to 40 pages are supported in this development build.");
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(ContentOrderTextExtractor.GetText(page));
            if (builder.Length > MaxCharacters) break;
        }
        return builder.ToString();
    }

    private static string ExtractDocx(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml")
            ?? throw new ArgumentException("This DOCX file has no readable Word document content.");
        if (entry.Length > 4_000_000)
            throw new ArgumentException("DOCX content is too large to process safely.");
        using var reader = entry.Open();
        var root = XDocument.Load(reader);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return string.Join("\n", root.Descendants(w + "p")
            .Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))));
    }
}
