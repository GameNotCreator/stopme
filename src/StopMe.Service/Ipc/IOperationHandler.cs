using StopMe.LocalApi;

namespace StopMe.Service.Ipc;

public interface IOperationHandler
{
    string Op { get; }

    Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct);
}
