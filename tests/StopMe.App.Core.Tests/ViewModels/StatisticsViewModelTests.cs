using FluentAssertions;
using StopMe.App.Core.Services;
using StopMe.App.Core.Tests.Fakes;
using StopMe.App.Core.ViewModels;
using StopMe.LocalApi.Dtos;
using Xunit;

namespace StopMe.App.Core.Tests.ViewModels;

public class StatisticsViewModelTests
{
    [Fact]
    public async Task Refresh_loads_summary_and_privacy_state()
    {
        var fake = new FakeServiceConnection
        {
            StatsResponseToReturn = SampleResponse("Anonymized"),
        };
        fake.SettingsToReturn["stats.privacy_mode"] = "Anonymized";

        var vm = NewViewModel(fake);
        await vm.RefreshAsync();

        vm.SessionsCompleted.Should().Be(3);
        vm.FocusMinutes.Should().Be(180);
        vm.WebsiteBlockCount.Should().Be(5);
        vm.AppBlockCount.Should().Be(2);
        vm.BypassAttemptCount.Should().Be(1);
        vm.ProductiveDayStreak.Should().Be(2);
        vm.Daily.Should().HaveCount(1);
        vm.TopDistractions.Should().HaveCount(1);
        vm.SelectedPrivacyMode.Should().Be("Anonymized");
    }

    [Fact]
    public async Task ApplyPrivacy_pushes_to_service_then_refreshes_stats()
    {
        var fake = new FakeServiceConnection
        {
            StatsResponseToReturn = SampleResponse("Readable"),
        };
        var vm = NewViewModel(fake);
        vm.SelectedPrivacyMode = "Anonymized";

        await vm.ApplyPrivacyAsync();

        fake.SettingsSets.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new KeyValuePair<string, string>("stats.privacy_mode", "Anonymized"));
        fake.LastStatsPeriodRequested.Should().NotBeNull();
    }

    [Fact]
    public async Task ExportCsv_writes_response_via_csv_writer()
    {
        var fake = new FakeServiceConnection
        {
            StatsExportResponseToReturn = new StatsExportResponse
            {
                Filename = "stopme-stats-test.csv",
                CsvText = "summary\n",
            },
        };
        var writer = new RecordingCsvWriter();
        var vm = new StatisticsViewModel(fake, writer);

        await vm.ExportCsvAsync();

        writer.Saved.Should().ContainSingle();
        writer.Saved[0].filename.Should().Be("stopme-stats-test.csv");
        writer.Saved[0].csv.Should().Contain("summary");
        vm.LastExportPath.Should().NotBeNull();
    }

    [Fact]
    public async Task Reset_clears_stats_and_reloads_summary()
    {
        var fake = new FakeServiceConnection
        {
            StatsResetCount = 42,
            StatsResponseToReturn = SampleResponse("Readable"),
        };
        var vm = NewViewModel(fake);

        await vm.ResetAsync();

        vm.LastResetSummary.Should().Contain("42");
        fake.StatsResetInvocations.Should().Be(1);
        fake.LastStatsPeriodRequested.Should().NotBeNull();
    }

    [Fact]
    public async Task Refresh_sets_error_when_service_unreachable()
    {
        var fake = new FakeServiceConnection { LastError = "no service" };
        var vm = NewViewModel(fake);

        await vm.RefreshAsync();

        vm.HasError.Should().BeTrue();
        vm.StatusMessage.Should().Be("no service");
    }

    [Fact]
    public void Selected_period_description_translates_period_token()
    {
        var vm = NewViewModel(new FakeServiceConnection());
        vm.SelectedPeriod = "Last30Days";
        vm.SelectedPeriodDescription.Should().Be("Last 30 days");
    }

    private static StatisticsViewModel NewViewModel(FakeServiceConnection fake) =>
        new(fake, new RecordingCsvWriter());

    private static StatsSummaryResponse SampleResponse(string privacy) => new()
    {
        Period = "Last7Days",
        FromUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        ToUtc = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc),
        Summary = new StatsSummaryDto
        {
            SessionsCompleted = 3,
            FocusMinutes = 180,
            WebsiteBlockCount = 5,
            AppBlockCount = 2,
            BypassAttemptCount = 1,
            ProductiveDayStreak = 2,
        },
        Daily = new[]
        {
            new StatsDailyBucketDto { Date = "2026-06-07", FocusMinutes = 60, WebsiteBlockCount = 3, AppBlockCount = 1, SessionsCompleted = 1 },
        },
        TopDistractions = new[]
        {
            new StatsTopDistractionDto { Label = "youtube.com", BlockCount = 3, Kind = "Website" },
        },
        PrivacyMode = privacy,
    };

    private sealed class RecordingCsvWriter : IStatsCsvWriter
    {
        public List<(string filename, string csv)> Saved { get; } = new();

        public string SaveCsv(string filename, string csvText)
        {
            Saved.Add((filename, csvText));
            return "/fake/" + filename;
        }
    }
}
