namespace StopMe.App.Core.Models;

public sealed record NavigationItem(string Key, string Label, string IconGlyph, bool IsFooter = false);
