using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.State;

namespace StopMe.Service.Ipc.Handlers;

// Appends one or more rules to an existing blocklist. Used by the "Block Apps"
// page to add process-name rules without recreating the blocklist.
public sealed class AddRuleHandler : IOperationHandler
{
    private readonly IBlocklistRepository _blocklists;
    private readonly IStrictModeGuard _guard;
    private readonly ServiceState _state;
    private readonly TimeProvider _clock;

    public AddRuleHandler(
        IBlocklistRepository blocklists,
        IStrictModeGuard guard,
        ServiceState state,
        TimeProvider clock)
    {
        _blocklists = blocklists;
        _guard = guard;
        _state = state;
        _clock = clock;
    }

    public string Op => Operations.AddRule;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<AddRuleRequest>();
        if (dto is null) return LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing payload.");
        if (dto.Rules.Count == 0) return LocalApiResponse.Error(ErrorCodes.BadRequest, "No rules to add.");

        var existing = await _blocklists.GetByIdAsync(dto.BlocklistId, ct).ConfigureAwait(false);
        if (existing is null) return LocalApiResponse.Error(ErrorCodes.NotFound, "Blocklist not found.");

        var decision = _guard.CanMutateBlocklist(existing.Id, _state.Current.ActiveSessions);
        if (!decision.IsAllowed)
            return LocalApiResponse.Error(ErrorCodes.Conflict, decision.DenialReason ?? "Strict-mode session locks this blocklist.");

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var added = new List<BlockRule>();

        foreach (var rule in dto.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Value)) continue;
            if (!Enum.TryParse<RuleType>(rule.Type, ignoreCase: true, out var ruleType))
                return LocalApiResponse.Error(ErrorCodes.BadRequest, $"Unknown rule type '{rule.Type}'.");

            added.Add(new BlockRule
            {
                Id = Guid.NewGuid(),
                BlocklistId = existing.Id,
                Type = ruleType,
                Value = rule.Value.Trim(),
                IsEnabled = true,
                Notes = rule.Notes,
                CreatedAtUtc = nowUtc,
            });
        }

        await _blocklists.AppendRulesAsync(existing.Id, added, nowUtc, ct).ConfigureAwait(false);

        return LocalApiResponse.Ok(new AddRuleResponse
        {
            AddedCount = added.Count,
            Rules = added.Select(Mapping.ToDto).ToArray(),
        });
    }
}
