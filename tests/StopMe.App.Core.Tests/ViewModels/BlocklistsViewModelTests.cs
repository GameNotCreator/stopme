using FluentAssertions;
using StopMe.App.Core.Tests.Fakes;
using StopMe.App.Core.ViewModels;
using StopMe.LocalApi.Dtos;
using Xunit;

namespace StopMe.App.Core.Tests.ViewModels;

public class BlocklistsViewModelTests
{
    [Fact]
    public async Task Refresh_loads_blocklists_and_selects_first()
    {
        var fake = new FakeServiceConnection();
        fake.BlocklistsToReturn.Add(MakeBlocklist("Social"));
        fake.BlocklistsToReturn.Add(MakeBlocklist("Work"));

        var vm = new BlocklistsViewModel(fake);
        await vm.RefreshAsync();

        vm.Blocklists.Should().HaveCount(2);
        vm.SelectedBlocklist.Should().NotBeNull();
        vm.HasSelection.Should().BeTrue();
    }

    [Fact]
    public async Task Create_calls_service_and_appends_to_list()
    {
        var fake = new FakeServiceConnection();
        var vm = new BlocklistsViewModel(fake)
        {
            NewName = "Distractions",
            NewRulesText = "youtube.com\n*.tiktok.com\nallow:docs.google.com",
        };

        await vm.CreateAsync();

        fake.Created.Should().ContainSingle();
        fake.Created[0].Name.Should().Be("Distractions");
        fake.Created[0].Rules.Should().HaveCount(3);
        fake.Created[0].Rules![0].Type.Should().Be("Domain");
        fake.Created[0].Rules![1].Type.Should().Be("Wildcard");
        fake.Created[0].Rules![2].Type.Should().Be("ExceptionDomain");
        vm.Blocklists.Should().ContainSingle();
        vm.NewName.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteSelected_removes_from_list()
    {
        var fake = new FakeServiceConnection();
        var b = MakeBlocklist("Doomed");
        fake.BlocklistsToReturn.Add(b);

        var vm = new BlocklistsViewModel(fake);
        await vm.RefreshAsync();
        vm.SelectedBlocklist = vm.Blocklists.First();

        await vm.DeleteSelectedAsync();

        fake.Deleted.Should().ContainSingle().Which.Should().Be(b.Id);
        vm.Blocklists.Should().BeEmpty();
    }

    [Theory]
    [InlineData("youtube.com", "Domain")]
    [InlineData("*.youtube.com", "Wildcard")]
    [InlineData("reddit.com/r/funny", "Url")]
    [InlineData("allow:docs.google.com", "ExceptionDomain")]
    [InlineData("allow:reddit.com/r/learnprogramming", "ExceptionUrl")]
    [InlineData("allow:*.github.com", "ExceptionWildcard")]
    [InlineData("category:Social Media", "Category")]
    [InlineData("*.*", "FullInternet")]
    public void ParseRules_categorizes_each_line(string input, string expectedType)
    {
        var rules = BlocklistsViewModel.ParseRules(input);
        rules.Should().ContainSingle().Which.Type.Should().Be(expectedType);
    }

    [Fact]
    public void ParseRules_skips_comments_and_blanks()
    {
        var input = "# heading\n\nyoutube.com\n! comment\nfacebook.com\n";
        BlocklistsViewModel.ParseRules(input).Should().HaveCount(2);
    }

    private static BlocklistDto MakeBlocklist(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Mode = "Blacklist",
        IsEnabled = true,
        Rules = Array.Empty<BlockRuleDto>(),
    };
}
