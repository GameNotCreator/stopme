using StopMe.Core.Abstractions;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Ipc.Handlers;

// Creates a one-shot FocusSession from the UI. The "Quick Start" flow uses this.
public sealed class CreateSessionHandler : IOperationHandler
{
    private readonly ISessionRepository _sessions;
    private readonly ISessionEngine _engine;
    private readonly IUserContext _user;
    private readonly TimeProvider _clock;

    public CreateSessionHandler(
        ISessionRepository sessions,
        ISessionEngine engine,
        IUserContext user,
        TimeProvider clock)
    {
        _sessions = sessions;
        _engine = engine;
        _user = user;
        _clock = clock;
    }

    public string Op => Operations.CreateSession;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<CreateSessionRequest>();
        if (dto is null) return LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing payload.");
        if (string.IsNullOrWhiteSpace(dto.Name)) return LocalApiResponse.Error(ErrorCodes.BadRequest, "Session name is required.");
        if (dto.EndAtUtc <= dto.StartAtUtc) return LocalApiResponse.Error(ErrorCodes.BadRequest, "End time must be after start time.");

        if (!Enum.TryParse<SessionMode>(dto.Mode, ignoreCase: true, out var mode))
            return LocalApiResponse.Error(ErrorCodes.BadRequest, $"Unknown mode '{dto.Mode}'.");
        if (!Enum.TryParse<LockPolicy>(dto.LockPolicy, ignoreCase: true, out var lockPolicy))
            return LocalApiResponse.Error(ErrorCodes.BadRequest, $"Unknown lock policy '{dto.LockPolicy}'.");

        if (lockPolicy == LockPolicy.Password && string.IsNullOrEmpty(dto.Password))
            return LocalApiResponse.Error(ErrorCodes.BadRequest, "Password lock policy requires a password.");
        if (lockPolicy == LockPolicy.RandomText && string.IsNullOrEmpty(dto.RequiredText))
            return LocalApiResponse.Error(ErrorCodes.BadRequest, "RandomText lock policy requires a phrase.");

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var session = new FocusSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = _user.CurrentUserProfileId,
            Name = dto.Name.Trim(),
            StartAtUtc = dto.StartAtUtc,
            EndAtUtc = dto.EndAtUtc,
            Status = SessionStatus.Scheduled,
            Mode = mode,
            LockPolicy = lockPolicy,
            TimeZoneId = TimeZoneInfo.Local.Id,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            PasswordHash = string.IsNullOrEmpty(dto.Password) ? null : PasswordHasher.Hash(dto.Password!),
            RequiredText = string.IsNullOrEmpty(dto.RequiredText) ? null : dto.RequiredText,
            SessionBlocklists = dto.BlocklistIds
                .Distinct()
                .Select(id => new SessionBlocklist { BlocklistId = id })
                .ToList(),
        };

        await _sessions.AddAsync(session, ct).ConfigureAwait(false);

        var started = false;
        string? warning = null;

        if (dto.StartImmediately)
        {
            if (dto.StartAtUtc > nowUtc)
            {
                warning = "Session created but will start at its scheduled time, not now.";
            }
            else
            {
                try
                {
                    await _engine.StartSessionAsync(session.Id, nowUtc, ct).ConfigureAwait(false);
                    started = true;
                }
                catch (InvalidOperationException ex)
                {
                    warning = ex.Message;
                }
            }
        }

        return LocalApiResponse.Ok(new CreateSessionResponse
        {
            SessionId = session.Id,
            Started = started,
            Warning = warning,
        });
    }
}
