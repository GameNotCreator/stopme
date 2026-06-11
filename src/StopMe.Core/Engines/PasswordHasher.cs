using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StopMe.Core.Engines;

// PBKDF2-SHA512 over Rfc2898DeriveBytes. New hashes are produced in the format:
//   pbkdf2-sha512$v=1$i=200000$salt=<base64>$hash=<base64>
// Verify also accepts the legacy SHA-256 placeholder format (64 hex chars) for
// backwards compatibility, but always returns false for it — that way any user
// whose password was hashed under the placeholder is forced to reset rather
// than silently relying on the weak scheme.
public static class PasswordHasher
{
    private const string Scheme = "pbkdf2-sha512";
    private const int Version = 1;
    private const int DefaultIterations = 200_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 64; // matches SHA-512 output

    public static string Hash(string password) => Hash(password, DefaultIterations);

    public static string Hash(string password, int iterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        if (iterations < 10_000) throw new ArgumentOutOfRangeException(nameof(iterations), "Must be ≥ 10000.");

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA512,
            outputLength: HashBytes);

        return string.Create(CultureInfo.InvariantCulture,
            $"{Scheme}$v={Version}$i={iterations}$salt={Convert.ToBase64String(salt)}$hash={Convert.ToBase64String(hash)}");
    }

    public static bool Verify(string? candidate, string? expectedHash)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(expectedHash)) return false;

        if (!TryParse(expectedHash, out var parts))
        {
            // Legacy SHA-256 placeholder: 64 lowercase/uppercase hex chars. We DO NOT
            // verify against the legacy scheme on purpose — force the user to reset.
            return false;
        }

        var candidateHash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(candidate),
            salt: parts.Salt,
            iterations: parts.Iterations,
            hashAlgorithm: HashAlgorithmName.SHA512,
            outputLength: parts.Hash.Length);

        return CryptographicOperations.FixedTimeEquals(candidateHash, parts.Hash);
    }

    public static bool IsLegacyHash(string? hash) =>
        !string.IsNullOrEmpty(hash) && hash!.Length == 64
        && hash.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));

    private static bool TryParse(string serialized, out ParsedHash parts)
    {
        parts = default;
        if (!serialized.StartsWith(Scheme + "$", StringComparison.Ordinal)) return false;

        var segments = serialized.Split('$');
        if (segments.Length != 5) return false;

        if (!TryReadField(segments[1], "v=", out var versionRaw)) return false;
        if (!int.TryParse(versionRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)) return false;
        if (version != Version) return false;

        if (!TryReadField(segments[2], "i=", out var iterRaw)) return false;
        if (!int.TryParse(iterRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations)) return false;
        if (iterations < 1) return false;

        if (!TryReadField(segments[3], "salt=", out var saltRaw)) return false;
        if (!TryReadField(segments[4], "hash=", out var hashRaw)) return false;

        try
        {
            parts = new ParsedHash
            {
                Iterations = iterations,
                Salt = Convert.FromBase64String(saltRaw),
                Hash = Convert.FromBase64String(hashRaw),
            };
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryReadField(string segment, string prefix, out string value)
    {
        if (!segment.StartsWith(prefix, StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }
        value = segment[prefix.Length..];
        return true;
    }

    private struct ParsedHash
    {
        public int Iterations;
        public byte[] Salt;
        public byte[] Hash;
    }
}
