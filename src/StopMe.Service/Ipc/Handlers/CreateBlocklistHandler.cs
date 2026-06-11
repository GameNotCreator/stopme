using StopMe.Core.Abstractions;
using StopMe.Core.Models;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Ipc.Handlers;

public sealed class CreateBlocklistHandler : IOperationHandler
{
    private readonly IBlocklistRepository _repo;
    private readonly IUserContext _user;
    private readonly TimeProvider _clock;

    public CreateBlocklistHandler(IBlocklistRepository repo, IUserContext user, TimeProvider clock)
    {
        _repo = repo;
        _user = user;
        _clock = clock;
    }

    public string Op => Operations.CreateBlocklist;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<CreateBlocklistRequest>();
        if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
            return LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing or invalid name.");

        if (!Mapping.TryParseBlocklistMode(dto.Mode, out var mode))
            return LocalApiResponse.Error(ErrorCodes.BadRequest, $"Invalid mode '{dto.Mode}'.");

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var blocklist = new Blocklist
        {
            Id = Guid.NewGuid(),
            UserProfileId = _user.CurrentUserProfileId,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Mode = mode,
            IsEnabled = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };

        if (dto.Rules is { Count: > 0 })
        {
            foreach (var rule in dto.Rules)
            {
                if (!Mapping.TryParseRuleType(rule.Type, out var type))
                    return LocalApiResponse.Error(ErrorCodes.BadRequest, $"Invalid rule type '{rule.Type}'.");
                if (string.IsNullOrWhiteSpace(rule.Value))
                    return LocalApiResponse.Error(ErrorCodes.BadRequest, "Rule value is empty.");

                blocklist.Rules.Add(new BlockRule
                {
                    Id = Guid.NewGuid(),
                    BlocklistId = blocklist.Id,
                    Type = type,
                    Value = rule.Value.Trim(),
                    Notes = rule.Notes,
                    CreatedAtUtc = nowUtc,
                });
            }
        }

        await _repo.AddAsync(blocklist, ct).ConfigureAwait(false);
        return LocalApiResponse.Ok(Mapping.ToDto(blocklist));
    }
}
