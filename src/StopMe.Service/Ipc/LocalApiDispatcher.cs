using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StopMe.LocalApi;

namespace StopMe.Service.Ipc;

public sealed class LocalApiDispatcher
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IClientAuthenticator _auth;
    private readonly ILogger<LocalApiDispatcher> _logger;

    public LocalApiDispatcher(IServiceScopeFactory scopes, IClientAuthenticator auth, ILogger<LocalApiDispatcher> logger)
    {
        _scopes = scopes;
        _auth = auth;
        _logger = logger;
    }

    public async Task<LocalApiResponse> DispatchAsync(LocalApiRequest? request, CancellationToken ct)
    {
        if (request is null) return LocalApiResponse.Error(ErrorCodes.BadRequest, "Empty request.");
        if (!_auth.IsAuthorized(request.Token)) return LocalApiResponse.Error(ErrorCodes.Unauthorized, "Invalid or missing token.");
        if (string.IsNullOrWhiteSpace(request.Op)) return LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing operation.");

        await using var scope = _scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider
            .GetServices<IOperationHandler>()
            .FirstOrDefault(h => string.Equals(h.Op, request.Op, StringComparison.Ordinal));

        if (handler is null)
            return LocalApiResponse.Error(ErrorCodes.UnknownOperation, $"Unknown operation '{request.Op}'.");

        try
        {
            return await handler.HandleAsync(request, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation {Op} failed", request.Op);
            return LocalApiResponse.Error(ErrorCodes.Internal, "Internal service error.");
        }
    }
}
