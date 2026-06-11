using System.Security.Cryptography;

namespace StopMe.LocalApi.Client;

public sealed class TokenStore
{
    public const string TokenFileName = "auth.token";

    private readonly string _path;

    public TokenStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, TokenFileName);
    }

    public string LoadOrCreate()
    {
        if (File.Exists(_path))
        {
            var existing = File.ReadAllText(_path).Trim();
            if (existing.Length > 0) return existing;
        }

        var token = Generate();
        File.WriteAllText(_path, token);
        return token;
    }

    public string? TryLoad()
    {
        if (!File.Exists(_path)) return null;
        var text = File.ReadAllText(_path).Trim();
        return text.Length == 0 ? null : text;
    }

    public string FilePath => _path;

    private static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
