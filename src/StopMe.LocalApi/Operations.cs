namespace StopMe.LocalApi;

public static class Operations
{
    public const string Status = "status";
    public const string ActiveSessions = "sessions.active";
    public const string SessionList = "sessions.list";
    public const string StartSession = "sessions.start";
    public const string StopSession = "sessions.stop";
    public const string ListBlocklists = "blocklists.list";
    public const string CreateBlocklist = "blocklists.create";
    public const string UpdateBlocklist = "blocklists.update";
    public const string DeleteBlocklist = "blocklists.delete";
    public const string EvaluateUrl = "rules.evaluate_url";
    public const string EvaluateApp = "rules.evaluate_app";
    public const string RuleSnapshot = "rules.snapshot";
    public const string ExtensionHeartbeat = "extension.heartbeat";
    public const string ExtensionHealth = "extension.health";
    public const string BypassEvents = "bypass.events";
    public const string StatsSummary = "stats.summary";
    public const string StatsExportCsv = "stats.export_csv";
    public const string StatsReset = "stats.reset";
    public const string SettingsGet = "settings.get";
    public const string SettingsSet = "settings.set";
    public const string TemplatesList = "templates.list";
    public const string TemplatesApply = "templates.apply";
    public const string ProcessSnapshot = "processes.snapshot";
    public const string CreateSession = "sessions.create";
    public const string AddRule = "blocklists.add_rule";
    public const string InstalledApps = "apps.installed";
    public const string SearchDomains = "websites.search";
}

public static class LocalApiProtocol
{
    public const string PipeName = "StopMe.LocalApi.v1";
    public const int CurrentVersion = 1;
    public const int MaxMessageBytes = 8 * 1024 * 1024; // 8 MiB
}

public static class ErrorCodes
{
    public const string Unauthorized = "unauthorized";
    public const string UnknownOperation = "unknown_op";
    public const string BadRequest = "bad_request";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string Internal = "internal";
}
