using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Engines;

public sealed class AppRuleEngine : IAppRuleEngine
{
    public AppBlockDecision Evaluate(RunningAppInfo app, IReadOnlyList<CompiledRule> activeRules)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(activeRules);

        foreach (var rule in activeRules)
        {
            if (!IsAppRule(rule.Type)) continue;

            if (Matches(rule, app))
                return AppBlockDecision.Block(
                    reason: BuildReason(rule, app),
                    ruleId: rule.RuleId,
                    ruleType: rule.Type,
                    sessionId: rule.SessionId,
                    sessionName: rule.SessionName);
        }

        return AppBlockDecision.Allow();
    }

    private static bool IsAppRule(RuleType type) =>
        type is RuleType.WindowsProcessName
            or RuleType.WindowsExecutablePath
            or RuleType.WindowsFolderPath
            or RuleType.WindowTitle
            or RuleType.MicrosoftStoreApp;

    private static bool Matches(CompiledRule rule, RunningAppInfo app) =>
        rule.Type switch
        {
            RuleType.WindowsProcessName => MatchProcessName(rule.Value, app.ProcessName),
            RuleType.WindowsExecutablePath => MatchExecutablePath(rule.Value, app.ExecutablePath),
            RuleType.WindowsFolderPath => MatchFolderPath(rule.Value, app.ExecutablePath),
            RuleType.WindowTitle => MatchWindowTitle(rule.Value, app.MainWindowTitle),
            RuleType.MicrosoftStoreApp => MatchStoreApp(rule.Value, app),
            _ => false,
        };

    private static bool MatchProcessName(string ruleValue, string processName)
    {
        var rule = NormalizeProcessName(ruleValue);
        var actual = NormalizeProcessName(processName);
        return rule.Length > 0 && string.Equals(rule, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchExecutablePath(string ruleValue, string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return false;
        var rule = NormalizePath(ruleValue);
        var actual = NormalizePath(executablePath);
        return string.Equals(rule, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchFolderPath(string ruleValue, string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return false;
        var folder = NormalizePath(ruleValue);
        var actual = NormalizePath(executablePath);
        if (folder.Length == 0) return false;
        if (!folder.EndsWith('/')) folder += "/";
        return actual.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchWindowTitle(string ruleValue, string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        return title.Contains(ruleValue.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchStoreApp(string ruleValue, RunningAppInfo app) =>
        !string.IsNullOrEmpty(app.PackageFamilyName)
        && string.Equals(app.PackageFamilyName, ruleValue.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeProcessName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^4];
        return trimmed;
    }

    private static string NormalizePath(string value) =>
        value.Trim().Replace('\\', '/');

    private static string BuildReason(CompiledRule rule, RunningAppInfo app)
    {
        var what = rule.Type switch
        {
            RuleType.WindowsProcessName => $"process '{app.ProcessName}'",
            RuleType.WindowsExecutablePath => $"executable '{app.ExecutablePath}'",
            RuleType.WindowsFolderPath => $"folder rule '{rule.Value}'",
            RuleType.WindowTitle => $"window title contains '{rule.Value}'",
            RuleType.MicrosoftStoreApp => $"Store app '{rule.Value}'",
            _ => "app rule",
        };

        return rule.SessionName is null
            ? $"Blocked {what}"
            : $"Blocked {what} during '{rule.SessionName}'";
    }
}
