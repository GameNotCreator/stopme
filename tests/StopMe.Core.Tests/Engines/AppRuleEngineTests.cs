using FluentAssertions;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using StopMe.Core.Tests.Fakes;
using StopMe.Core.ValueObjects;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class AppRuleEngineTests
{
    private readonly AppRuleEngine _engine = new();

    [Fact]
    public void Process_name_matches_with_or_without_exe_suffix()
    {
        var app = App("steam");
        var ruleWithExe = TestData.Rule(RuleType.WindowsProcessName, "steam.exe");
        _engine.Evaluate(app, new[] { ruleWithExe }).IsBlocked.Should().BeTrue();

        var ruleWithout = TestData.Rule(RuleType.WindowsProcessName, "STEAM");
        _engine.Evaluate(app, new[] { ruleWithout }).IsBlocked.Should().BeTrue();
    }

    [Fact]
    public void Process_name_no_match_returns_allow()
    {
        var app = App("notepad");
        var rule = TestData.Rule(RuleType.WindowsProcessName, "steam.exe");
        _engine.Evaluate(app, new[] { rule }).IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void Executable_path_exact_match()
    {
        var app = App("Discord", @"C:\Users\Bob\AppData\Local\Discord\Discord.exe");
        var rule = TestData.Rule(RuleType.WindowsExecutablePath, @"c:\users\bob\appdata\local\discord\discord.exe");
        _engine.Evaluate(app, new[] { rule }).IsBlocked.Should().BeTrue();
    }

    [Fact]
    public void Folder_path_matches_executables_within_folder()
    {
        var app = App("MyGame", @"C:\Games\Steam\MyGame.exe");
        var rule = TestData.Rule(RuleType.WindowsFolderPath, @"C:\Games");
        _engine.Evaluate(app, new[] { rule }).IsBlocked.Should().BeTrue();
    }

    [Fact]
    public void Folder_path_does_not_match_executables_outside()
    {
        var app = App("Outside", @"C:\Other\app.exe");
        var rule = TestData.Rule(RuleType.WindowsFolderPath, @"C:\Games");
        _engine.Evaluate(app, new[] { rule }).IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void Window_title_substring_match()
    {
        var app = App("chrome", title: "YouTube — Mozilla Firefox");
        var rule = TestData.Rule(RuleType.WindowTitle, "youtube");
        _engine.Evaluate(app, new[] { rule }).IsBlocked.Should().BeTrue();
    }

    [Fact]
    public void Microsoft_store_app_matches_by_package_family_name()
    {
        var app = new RunningAppInfo
        {
            ProcessId = 42,
            ProcessName = "ms-store-app",
            PackageFamilyName = "Microsoft.YourPhone_8wekyb3d8bbwe",
            IsMicrosoftStoreApp = true,
        };
        var rule = TestData.Rule(RuleType.MicrosoftStoreApp, "Microsoft.YourPhone_8wekyb3d8bbwe");
        _engine.Evaluate(app, new[] { rule }).IsBlocked.Should().BeTrue();
    }

    [Fact]
    public void Decision_carries_session_metadata()
    {
        var sessionId = Guid.NewGuid();
        var app = App("steam");
        var rule = TestData.Rule(RuleType.WindowsProcessName, "steam.exe",
            sessionId: sessionId,
            sessionName: "Work Hours");
        var decision = _engine.Evaluate(app, new[] { rule });
        decision.SessionId.Should().Be(sessionId);
        decision.SessionName.Should().Be("Work Hours");
    }

    [Fact]
    public void Url_rules_are_ignored_for_apps()
    {
        var app = App("chrome");
        var rule = TestData.Rule(RuleType.Domain, "youtube.com");
        _engine.Evaluate(app, new[] { rule }).IsBlocked.Should().BeFalse();
    }

    private static RunningAppInfo App(string name, string? path = null, string? title = null) =>
        new()
        {
            ProcessId = Random.Shared.Next(1, 99999),
            ProcessName = name,
            ExecutablePath = path,
            MainWindowTitle = title,
        };
}
