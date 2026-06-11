using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Engines;

// Pure data — the six default templates the spec lists. Keys are stable; the
// service references them when persisting blocklist names like
// "Template/study/Distractions" so re-applying the same template updates the
// existing blocklists rather than duplicating.
public static class TemplateCatalog
{
    public static IReadOnlyList<SessionTemplate> Defaults { get; } = new[]
    {
        new SessionTemplate
        {
            Key = "study",
            DisplayName = "Study Mode",
            Description = "Long focus block. Hides Social Media, Video Streaming, Gaming. Soft mode, no pauses.",
            Duration = TimeSpan.FromHours(2),
            Mode = SessionMode.Soft,
            LockPolicy = LockPolicy.Confirmation,
            Blocklists = new[]
            {
                new TemplateBlocklist
                {
                    Name = "Study — Distractions",
                    Mode = BlocklistMode.Blacklist,
                    Categories = new[] { "Social Media", "Video Streaming", "Gaming", "Entertainment" },
                },
            },
        },

        new SessionTemplate
        {
            Key = "work",
            DisplayName = "Work Mode",
            Description = "Office hours. Blocks social, news, shopping, messaging. Soft, confirmation to stop.",
            Duration = TimeSpan.FromHours(4),
            Mode = SessionMode.Soft,
            LockPolicy = LockPolicy.Confirmation,
            DefaultRecurrenceRule = "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR",
            Blocklists = new[]
            {
                new TemplateBlocklist
                {
                    Name = "Work — Distractions",
                    Mode = BlocklistMode.Blacklist,
                    Categories = new[] { "Social Media", "News", "Shopping", "Messaging" },
                },
            },
        },

        new SessionTemplate
        {
            Key = "sleep",
            DisplayName = "Sleep Mode",
            Description = "Evening wind-down. Blocks all video / social / AI chatbots. Strict — random text to break.",
            Duration = TimeSpan.FromHours(8),
            Mode = SessionMode.Strict,
            LockPolicy = LockPolicy.RandomText,
            DefaultRecurrenceRule = "FREQ=DAILY",
            Blocklists = new[]
            {
                new TemplateBlocklist
                {
                    Name = "Sleep — Quiet hours",
                    Mode = BlocklistMode.Blacklist,
                    Categories = new[] { "Social Media", "Video Streaming", "Gaming", "AI Chatbots", "Entertainment", "Messaging" },
                },
            },
        },

        new SessionTemplate
        {
            Key = "exam",
            DisplayName = "Exam Mode",
            Description = "Hard block on everything except your reference sites. Strict, time-locked.",
            Duration = TimeSpan.FromHours(3),
            Mode = SessionMode.Strict,
            LockPolicy = LockPolicy.TimeLocked,
            Blocklists = new[]
            {
                new TemplateBlocklist
                {
                    Name = "Exam — Allowed only",
                    Mode = BlocklistMode.Allowlist,
                    Categories = Array.Empty<string>(),
                    Domains = new[]
                    {
                        "docs.google.com",
                        "wikipedia.org",
                        "stackoverflow.com",
                        "github.com",
                    },
                },
            },
        },

        new SessionTemplate
        {
            Key = "deep-work",
            DisplayName = "Deep Work",
            Description = "Single 90-minute focus block. Blocks social, messaging, video, news. Strict, time-locked.",
            Duration = TimeSpan.FromMinutes(90),
            Mode = SessionMode.Strict,
            LockPolicy = LockPolicy.TimeLocked,
            Blocklists = new[]
            {
                new TemplateBlocklist
                {
                    Name = "Deep Work — Distractions",
                    Mode = BlocklistMode.Blacklist,
                    Categories = new[] { "Social Media", "Messaging", "Video Streaming", "News", "Entertainment" },
                },
            },
        },

        new SessionTemplate
        {
            Key = "writing",
            DisplayName = "Writing Mode",
            Description = "Allow docs + research only. Blocks social, video, gaming, messaging. Soft.",
            Duration = TimeSpan.FromHours(2),
            Mode = SessionMode.Soft,
            LockPolicy = LockPolicy.Confirmation,
            Blocklists = new[]
            {
                new TemplateBlocklist
                {
                    Name = "Writing — Allowed only",
                    Mode = BlocklistMode.Allowlist,
                    Categories = Array.Empty<string>(),
                    Domains = new[]
                    {
                        "docs.google.com",
                        "notion.so",
                        "obsidian.md",
                        "wikipedia.org",
                        "merriam-webster.com",
                    },
                },
            },
        },
    };
}
