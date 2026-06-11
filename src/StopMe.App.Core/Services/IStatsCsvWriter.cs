namespace StopMe.App.Core.Services;

// Abstracts the "write text to a known folder" step so the ViewModel stays
// pure. The WinUI app wires an implementation that resolves to the user's
// Documents/StopMe directory; tests use a fake that captures the path.
public interface IStatsCsvWriter
{
    string SaveCsv(string filename, string csvText);
}

public sealed class DocumentsStatsCsvWriter : IStatsCsvWriter
{
    private readonly string _folder;

    public DocumentsStatsCsvWriter(string? folder = null)
    {
        _folder = folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "StopMe", "Exports");
    }

    public string SaveCsv(string filename, string csvText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        Directory.CreateDirectory(_folder);
        var safe = string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        var fullPath = Path.Combine(_folder, safe);
        File.WriteAllText(fullPath, csvText);
        return fullPath;
    }
}
