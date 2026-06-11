using StopMe.Core.Abstractions;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Ipc.Handlers;

public sealed class SettingsGetHandler : IOperationHandler
{
    private readonly ISettingsRepository _settings;

    public SettingsGetHandler(ISettingsRepository settings) => _settings = settings;

    public string Op => Operations.SettingsGet;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<SettingsGetRequest>();
        if (dto is null || string.IsNullOrWhiteSpace(dto.Key))
        {
            var all = await _settings.GetAllAsync(ct).ConfigureAwait(false);
            return LocalApiResponse.Ok(new SettingsGetResponse { Entries = all });
        }

        var value = await _settings.GetAsync(dto.Key, ct).ConfigureAwait(false);
        var entries = value is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [dto.Key] = value };
        return LocalApiResponse.Ok(new SettingsGetResponse { Entries = entries });
    }
}

public sealed class SettingsSetHandler : IOperationHandler
{
    private readonly ISettingsRepository _settings;

    public SettingsSetHandler(ISettingsRepository settings) => _settings = settings;

    public string Op => Operations.SettingsSet;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<SettingsSetRequest>();
        if (dto is null || string.IsNullOrWhiteSpace(dto.Key))
            return LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing setting key.");

        await _settings.SetAsync(dto.Key, dto.Value ?? string.Empty, ct).ConfigureAwait(false);
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [dto.Key] = dto.Value ?? string.Empty,
        };
        return LocalApiResponse.Ok(new SettingsGetResponse { Entries = entries });
    }
}
