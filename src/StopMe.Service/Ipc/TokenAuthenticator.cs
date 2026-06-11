using System.Security.Cryptography;
using System.Text;
using StopMe.LocalApi.Client;

namespace StopMe.Service.Ipc;

public sealed class TokenAuthenticator : IClientAuthenticator
{
    private readonly byte[] _expected;

    public TokenAuthenticator(TokenStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _expected = Encoding.UTF8.GetBytes(store.LoadOrCreate());
    }

    public bool IsAuthorized(string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        var candidate = Encoding.UTF8.GetBytes(token);
        if (candidate.Length != _expected.Length) return false;
        return CryptographicOperations.FixedTimeEquals(candidate, _expected);
    }
}
