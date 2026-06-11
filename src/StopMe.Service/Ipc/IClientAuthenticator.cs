namespace StopMe.Service.Ipc;

public interface IClientAuthenticator
{
    bool IsAuthorized(string? token);
}
