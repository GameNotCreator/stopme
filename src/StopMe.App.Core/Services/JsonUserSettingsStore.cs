using System.Text.Json;
using StopMe.App.Core.Models;

namespace StopMe.App.Core.Services;

public sealed class JsonUserSettingsStore : IUserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;

    public JsonUserSettingsStore(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(directoryPath);
        Directory.CreateDirectory(directoryPath);
        _filePath = Path.Combine(directoryPath, "user-settings.json");
    }

    public AppSettingsState Load()
    {
        if (!File.Exists(_filePath)) return new AppSettingsState();
        try
        {
            using var stream = File.OpenRead(_filePath);
            return JsonSerializer.Deserialize<AppSettingsState>(stream, JsonOptions) ?? new AppSettingsState();
        }
        catch (IOException) { return new AppSettingsState(); }
        catch (JsonException) { return new AppSettingsState(); }
    }

    public void Save(AppSettingsState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var tmp = _filePath + ".tmp";
        using (var stream = File.Create(tmp))
        {
            JsonSerializer.Serialize(stream, state, JsonOptions);
        }
        File.Move(tmp, _filePath, overwrite: true);
    }
}
