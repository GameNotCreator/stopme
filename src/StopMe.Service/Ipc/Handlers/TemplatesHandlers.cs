using StopMe.Core.Abstractions;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Ipc.Handlers;

public sealed class TemplatesListHandler : IOperationHandler
{
    private readonly ITemplateService _templates;

    public TemplatesListHandler(ITemplateService templates) => _templates = templates;

    public string Op => Operations.TemplatesList;

    public Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var list = _templates.List().Select(t => new TemplateDto
        {
            Key = t.Key,
            DisplayName = t.DisplayName,
            Description = t.Description,
            DurationMinutes = (int)t.Duration.TotalMinutes,
            Mode = t.Mode.ToString(),
            LockPolicy = t.LockPolicy.ToString(),
            DefaultRecurrenceRule = t.DefaultRecurrenceRule,
            Blocklists = t.Blocklists.Select(b => new TemplateBlocklistDto
            {
                Name = b.Name,
                Mode = b.Mode.ToString(),
                Categories = b.Categories,
                Domains = b.Domains,
                ProcessNames = b.ProcessNames,
            }).ToArray(),
        }).ToArray();

        return Task.FromResult(LocalApiResponse.Ok(new TemplatesListResponse { Templates = list }));
    }
}

public sealed class TemplatesApplyHandler : IOperationHandler
{
    private readonly ITemplateService _templates;
    private readonly IUserContext _user;

    public TemplatesApplyHandler(ITemplateService templates, IUserContext user)
    {
        _templates = templates;
        _user = user;
    }

    public string Op => Operations.TemplatesApply;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<TemplateApplyRequest>();
        if (dto is null || string.IsNullOrWhiteSpace(dto.TemplateKey))
            return LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing templateKey.");

        var result = await _templates.ApplyAsync(dto.TemplateKey, dto.StartAtUtc, _user.CurrentUserProfileId, ct).ConfigureAwait(false);
        if (!result.Success)
            return LocalApiResponse.Error(ErrorCodes.NotFound, result.Error ?? "Template not found.");

        return LocalApiResponse.Ok(new TemplateApplyResponse
        {
            Success = true,
            SessionId = result.SessionId,
            BlocklistIds = result.BlocklistIds,
        });
    }
}
