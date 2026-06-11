using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.State;

namespace StopMe.Service.Ipc.Handlers;

public sealed class EvaluateUrlHandler : IOperationHandler
{
    private readonly IUrlRuleEngine _engine;
    private readonly ServiceState _state;
    private readonly IUsageEventRecorder _recorder;
    private readonly TimeProvider _clock;

    public EvaluateUrlHandler(IUrlRuleEngine engine, ServiceState state, IUsageEventRecorder recorder, TimeProvider clock)
    {
        _engine = engine;
        _state = state;
        _recorder = recorder;
        _clock = clock;
    }

    public string Op => Operations.EvaluateUrl;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<EvaluateUrlRequest>();
        if (dto is null || string.IsNullOrWhiteSpace(dto.Url))
            return LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing url.");

        var snapshot = _state.Current;
        var decision = _engine.Evaluate(dto.Url, snapshot.CompiledRules);

        if (decision.IsBlocked)
        {
            await _recorder.RecordAsync(new UsageEventDraft
            {
                Type = UsageEventType.WebsiteBlocked,
                SessionId = decision.SessionId,
                RuleId = decision.MatchedRuleId,
                DomainLabel = ExtractHost(dto.Url),
                DisplayLabel = ExtractHost(dto.Url),
            }, _clock.GetUtcNow().UtcDateTime, ct).ConfigureAwait(false);
        }

        return LocalApiResponse.Ok(Mapping.ToResponse(decision));
    }

    private static string? ExtractHost(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u)) return u.Host;
        return url;
    }
}
