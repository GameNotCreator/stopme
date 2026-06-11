namespace StopMe.Core.Enums;

public enum RuleType
{
    Domain = 0,
    Url = 1,
    Wildcard = 2,
    Category = 3,
    WindowsProcessName = 10,
    WindowsExecutablePath = 11,
    WindowsFolderPath = 12,
    WindowTitle = 13,
    MicrosoftStoreApp = 14,
    FullInternet = 20,
    ExceptionDomain = 30,
    ExceptionUrl = 31,
    ExceptionWildcard = 32,
}
