using Microsoft.AspNetCore.Http;
using RoleFit.Api.Services;

// Usage: dotnet run --project tests/RoleFit.ExportRoundtripChecks -- "path/to/file.pdf" "path/to/file.docx"
// This test reads local documents only; it does not upload any resume or call Groq.
if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project tests/RoleFit.ExportRoundtripChecks -- <resume.pdf> [resume.docx]");
    return 2;
}

var outcomes = new List<(string Type, int Characters)>();
foreach (var filePath in args)
{
    var path = Path.GetFullPath(filePath);
    var extension = Path.GetExtension(path).ToLowerInvariant();
    if (extension is not (".pdf" or ".docx" or ".txt"))
    {
        Console.Error.WriteLine($"FAIL: Unsupported file type {extension}");
        return 2;
    }
    if (!File.Exists(path))
    {
        Console.Error.WriteLine("FAIL: File does not exist: " + path);
        return 2;
    }

    try
    {
        await using var stream = File.OpenRead(path);
        var upload = new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(path));
        var extracted = await new DocumentReader().ReadAsync(upload, CancellationToken.None);
        outcomes.Add((extension, extracted.Length));
        Console.WriteLine($"PASS {extension.ToUpperInvariant()}: ATS reader extracted {extracted.Length:N0} characters.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {extension.ToUpperInvariant()}: ATS reader rejected file: {ex.Message}");
        return 1;
    }
}

if (outcomes.Count == 2 && outcomes[0].Type == outcomes[1].Type)
    Console.WriteLine("NOTE: You supplied two files with the same extension. For an export round-trip, test PDF and DOCX versions of the SAME profile.");
Console.WriteLine("PASS: All supplied documents are readable by the same extractor used for ATS uploads.");
Console.WriteLine("Manual checks still required: candidate details, project count, skill accuracy, layout/page breaks.");
return 0;
