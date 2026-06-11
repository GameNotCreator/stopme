using StopMe.Core.ValueObjects;

namespace StopMe.Core.Abstractions;

// Templates are pure recipes. The service decides at apply-time:
//  - which categories actually exist (skip unknowns gracefully)
//  - the StartAtUtc / EndAtUtc based on the user's request
//  - whether to commit as a one-shot or schedule recurring
public interface ITemplateService
{
    IReadOnlyList<SessionTemplate> List();

    SessionTemplate? FindByKey(string key);

    Task<TemplateApplicationResult> ApplyAsync(
        string templateKey,
        DateTime startAtUtc,
        Guid userProfileId,
        CancellationToken ct = default);
}

public sealed class TemplateApplicationResult
{
    public required bool Success { get; init; }
    public Guid? SessionId { get; init; }
    public IReadOnlyList<Guid> BlocklistIds { get; init; } = Array.Empty<Guid>();
    public string? Error { get; init; }
}
