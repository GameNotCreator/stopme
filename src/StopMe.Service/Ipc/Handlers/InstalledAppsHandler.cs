using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.Processes;

namespace StopMe.Service.Ipc.Handlers;

public sealed class InstalledAppsHandler : IOperationHandler
{
    private readonly IInstalledAppsProvider _provider;
    private readonly TimeProvider _clock;

    public InstalledAppsHandler(IInstalledAppsProvider provider, TimeProvider clock)
    {
        _provider = provider;
        _clock = clock;
    }

    public string Op => Operations.InstalledApps;

    public Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var apps = _provider.EnumerateInstalled()
            .Select(a => new InstalledAppInfoDto
            {
                DisplayName = a.DisplayName,
                Publisher = a.Publisher,
                Version = a.Version,
                ExecutablePath = a.ExecutablePath,
                ProcessName = a.ProcessName,
                Source = a.Source,
            })
            .ToArray();

        return Task.FromResult(LocalApiResponse.Ok(new InstalledAppsResponse
        {
            Apps = apps,
            CapturedAtUtc = _clock.GetUtcNow().UtcDateTime,
        }));
    }
}
