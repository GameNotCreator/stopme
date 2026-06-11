using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using StopMe.App.Core.Tests.Fakes;
using StopMe.App.Core.ViewModels;
using StopMe.LocalApi.Dtos;
using Xunit;

namespace StopMe.App.Core.Tests.ViewModels;

public class TemplatesViewModelTests
{
    private readonly FakeServiceConnection _fake = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 9, 14, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Refresh_populates_templates_and_auto_selects_first()
    {
        _fake.TemplatesToReturn.Add(SampleTemplate("study", "Study Mode"));
        _fake.TemplatesToReturn.Add(SampleTemplate("work", "Work Mode"));

        var vm = NewViewModel();
        await vm.RefreshAsync();

        vm.Templates.Should().HaveCount(2);
        vm.SelectedTemplate.Should().NotBeNull();
        vm.SelectedTemplate!.Key.Should().Be("study");
        vm.HasNoTemplates.Should().BeFalse();
    }

    [Fact]
    public async Task Apply_passes_template_key_and_current_utc()
    {
        _fake.TemplatesToReturn.Add(SampleTemplate("deep-work", "Deep Work"));
        _fake.ApplyTemplateResponseToReturn = new TemplateApplyResponse
        {
            Success = true,
            SessionId = Guid.NewGuid(),
            BlocklistIds = new[] { Guid.NewGuid() },
        };
        var vm = NewViewModel();
        await vm.RefreshAsync();

        await vm.ApplyAsync(vm.Templates[0]);

        _fake.ApplyTemplateCalls.Should().ContainSingle();
        _fake.ApplyTemplateCalls[0].Key.Should().Be("deep-work");
        _fake.ApplyTemplateCalls[0].StartAtUtc.Should().Be(_clock.GetUtcNow().UtcDateTime);
        vm.LastAppliedSummary.Should().Contain("Deep Work");
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task Apply_with_no_selection_sets_error()
    {
        var vm = NewViewModel();
        await vm.ApplyAsync(null);

        vm.HasError.Should().BeTrue();
        _fake.ApplyTemplateCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Apply_failure_sets_error_message()
    {
        _fake.TemplatesToReturn.Add(SampleTemplate("study", "Study Mode"));
        _fake.ApplyTemplateResponseToReturn = new TemplateApplyResponse
        {
            Success = false,
            Error = "category missing",
        };
        var vm = NewViewModel();
        await vm.RefreshAsync();

        await vm.ApplyAsync(vm.Templates[0]);

        vm.HasError.Should().BeTrue();
        vm.StatusMessage.Should().Contain("category missing");
    }

    [Fact]
    public async Task Apply_uses_scheduled_start_when_both_pickers_set()
    {
        _fake.TemplatesToReturn.Add(SampleTemplate("study", "Study Mode"));
        _fake.ApplyTemplateResponseToReturn = new TemplateApplyResponse
        {
            Success = true,
            SessionId = Guid.NewGuid(),
            BlocklistIds = Array.Empty<Guid>(),
        };
        var vm = NewViewModel();
        await vm.RefreshAsync();

        var localDate = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.FromHours(2));   // local date carrier
        var localTime = new TimeSpan(14, 30, 0);
        vm.ScheduledStartDate = localDate;
        vm.ScheduledStartTime = localTime;
        vm.HasScheduledStart.Should().BeTrue();

        await vm.ApplyAsync(vm.Templates[0]);

        _fake.ApplyTemplateCalls.Should().ContainSingle();
        var sentUtc = _fake.ApplyTemplateCalls[0].StartAtUtc;
        // Picker date + picker time, treated as local, converted to UTC.
        var expectedLocal = DateTime.SpecifyKind(localDate.LocalDateTime.Date.Add(localTime), DateTimeKind.Local);
        sentUtc.Should().Be(expectedLocal.ToUniversalTime());
    }

    [Fact]
    public void ClearSchedule_resets_both_pickers()
    {
        var vm = NewViewModel();
        vm.ScheduledStartDate = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
        vm.ScheduledStartTime = TimeSpan.FromHours(8);
        vm.HasScheduledStart.Should().BeTrue();

        vm.ClearScheduleCommand.Execute(null);

        vm.ScheduledStartDate.Should().BeNull();
        vm.ScheduledStartTime.Should().BeNull();
        vm.HasScheduledStart.Should().BeFalse();
    }

    [Fact]
    public void FormatBlocklistPreview_summarises_rule_counts()
    {
        var b = new TemplateBlocklistDto
        {
            Name = "n",
            Mode = "Blacklist",
            Categories = new[] { "Social Media", "Gaming" },
            Domains = new[] { "x.com" },
            ProcessNames = Array.Empty<string>(),
        };
        TemplatesViewModel.FormatBlocklistPreview(b).Should().Be("2 categories, 1 domain");

        var empty = new TemplateBlocklistDto
        {
            Name = "n", Mode = "Blacklist",
            Categories = Array.Empty<string>(),
            Domains = Array.Empty<string>(),
            ProcessNames = Array.Empty<string>(),
        };
        TemplatesViewModel.FormatBlocklistPreview(empty).Should().Be("(no rules)");
    }

    private TemplatesViewModel NewViewModel() => new(_fake, _clock);

    private static TemplateDto SampleTemplate(string key, string name) => new()
    {
        Key = key,
        DisplayName = name,
        Description = $"Template {name}",
        DurationMinutes = 60,
        Mode = "Soft",
        LockPolicy = "None",
        Blocklists = new[]
        {
            new TemplateBlocklistDto
            {
                Name = name + " — Distractions",
                Mode = "Blacklist",
                Categories = new[] { "Social Media" },
                Domains = Array.Empty<string>(),
                ProcessNames = Array.Empty<string>(),
            },
        },
    };
}
