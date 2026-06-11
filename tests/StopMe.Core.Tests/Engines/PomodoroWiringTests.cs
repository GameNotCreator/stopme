using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StopMe.Core.Abstractions;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.Tests.Fakes;
using StopMe.Core.ValueObjects;
using Xunit;

namespace StopMe.Core.Tests.Engines;

// End-to-end coverage for the Phase-8 follow-up: a session in a Pomodoro Break
// is reported by SessionEngine with PausePhase=Break but its rules are dropped
// by RuleCompiler.
public class PomodoroWiringTests
{
    private readonly FakeSessionRepository _sessions = new();
    private readonly StubBlocklistRepository _blocklists = new();
    private readonly StubCategoryRepository _categories = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 9, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task SessionEngine_attaches_focus_phase_during_pomodoro_focus_window()
    {
        var sessionStart = _clock.GetUtcNow().UtcDateTime;
        var session = NewPomodoroSession(sessionStart, breakOffsetMinutes: 10);
        _sessions.Sessions.Add(session);

        var engine = NewEngine();
        // 5 minutes into a 25-minute focus block: still Focus
        _clock.Advance(TimeSpan.FromMinutes(5));
        var active = await engine.GetActiveSessionsAsync(_clock.GetUtcNow().UtcDateTime);

        active.Should().ContainSingle();
        active[0].PausePhase.Should().Be(PausePhase.Focus);
        active[0].IsOnBreak.Should().BeFalse();
        active[0].PausePhaseRemainingSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SessionEngine_attaches_break_phase_during_pomodoro_break_window()
    {
        var sessionStart = _clock.GetUtcNow().UtcDateTime;
        var session = NewPomodoroSession(sessionStart, breakOffsetMinutes: 10);
        _sessions.Sessions.Add(session);

        var engine = NewEngine();
        // 26 minutes into the cycle: 25 focus + 1 break = currently 1 min into break
        _clock.Advance(TimeSpan.FromMinutes(26));
        var active = await engine.GetActiveSessionsAsync(_clock.GetUtcNow().UtcDateTime);

        active.Should().ContainSingle();
        active[0].PausePhase.Should().Be(PausePhase.Break);
        active[0].IsOnBreak.Should().BeTrue();
    }

    [Fact]
    public async Task SessionEngine_leaves_phase_null_when_session_has_no_pause_policy()
    {
        var sessionStart = _clock.GetUtcNow().UtcDateTime;
        var session = NewSimpleSession(sessionStart);
        _sessions.Sessions.Add(session);

        var engine = NewEngine();
        var active = await engine.GetActiveSessionsAsync(sessionStart);

        active.Should().ContainSingle();
        active[0].PausePhase.Should().BeNull();
        active[0].IsOnBreak.Should().BeFalse();
    }

    [Fact]
    public async Task RuleCompiler_skips_rules_for_on_break_sessions()
    {
        var sessionId = Guid.NewGuid();
        var blocklistId = Guid.NewGuid();
        _blocklists.Add(new Blocklist
        {
            Id = blocklistId,
            Mode = BlocklistMode.Blacklist,
            IsEnabled = true,
            Rules =
            {
                new BlockRule { Type = RuleType.Domain, Value = "youtube.com", IsEnabled = true },
            },
        });

        var active = new[]
        {
            new ActiveSessionInstance
            {
                SessionId = sessionId,
                Name = "Pomodoro",
                StartAtUtc = DateTime.UtcNow,
                EndAtUtc = DateTime.UtcNow.AddHours(1),
                Mode = SessionMode.Soft,
                LockPolicy = LockPolicy.None,
                BlocklistIds = new[] { blocklistId },
                PausePhase = PausePhase.Break,
                PausePhaseRemainingSeconds = 240,
            },
        };

        var compiler = new RuleCompiler(_blocklists, _categories);
        var rules = await compiler.CompileForSessionsAsync(active);

        rules.Should().BeEmpty();
    }

    [Fact]
    public async Task RuleCompiler_keeps_rules_for_focus_phase_sessions()
    {
        var sessionId = Guid.NewGuid();
        var blocklistId = Guid.NewGuid();
        _blocklists.Add(new Blocklist
        {
            Id = blocklistId,
            Mode = BlocklistMode.Blacklist,
            IsEnabled = true,
            Rules =
            {
                new BlockRule { Type = RuleType.Domain, Value = "youtube.com", IsEnabled = true },
            },
        });

        var active = new[]
        {
            new ActiveSessionInstance
            {
                SessionId = sessionId,
                Name = "Pomodoro",
                StartAtUtc = DateTime.UtcNow,
                EndAtUtc = DateTime.UtcNow.AddHours(1),
                Mode = SessionMode.Soft,
                LockPolicy = LockPolicy.None,
                BlocklistIds = new[] { blocklistId },
                PausePhase = PausePhase.Focus,
                PausePhaseRemainingSeconds = 1500,
            },
        };

        var compiler = new RuleCompiler(_blocklists, _categories);
        var rules = await compiler.CompileForSessionsAsync(active);

        rules.Should().ContainSingle().Which.Value.Should().Be("youtube.com");
    }

    private SessionEngine NewEngine() =>
        new(_sessions, _clock, NullLogger<SessionEngine>.Instance, recorder: null, pauseEngine: new PomodoroPauseEngine());

    private static FocusSession NewPomodoroSession(DateTime startUtc, int breakOffsetMinutes)
    {
        var policy = new PausePolicy
        {
            Type = PauseType.Pomodoro,
            FocusMinutes = 25,
            BreakMinutes = 5,
            MaxPausesPerSession = 4,
            PauseDurationMinutes = 15,
        };
        return new FocusSession
        {
            Id = Guid.NewGuid(),
            Name = "Pomodoro",
            StartAtUtc = startUtc,
            EndAtUtc = startUtc.AddHours(2),
            Status = SessionStatus.Active,
            Mode = SessionMode.Soft,
            LockPolicy = LockPolicy.None,
            TimeZoneId = "UTC",
            CreatedAtUtc = startUtc,
            UpdatedAtUtc = startUtc,
            AllowPauses = true,
            PausePolicy = policy,
            PausePolicyId = policy.Id,
        };
    }

    private static FocusSession NewSimpleSession(DateTime startUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Simple",
            StartAtUtc = startUtc,
            EndAtUtc = startUtc.AddHours(1),
            Status = SessionStatus.Active,
            Mode = SessionMode.Soft,
            LockPolicy = LockPolicy.None,
            TimeZoneId = "UTC",
            CreatedAtUtc = startUtc,
            UpdatedAtUtc = startUtc,
            AllowPauses = false,
        };

    private sealed class StubBlocklistRepository : IBlocklistRepository
    {
        private readonly List<Blocklist> _store = new();
        public void Add(Blocklist b) => _store.Add(b);
        public Task<IReadOnlyList<Blocklist>> GetAllAsync(Guid u, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Blocklist>>(_store.Where(b => b.UserProfileId == u).ToList());
        public Task<IReadOnlyList<Blocklist>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Blocklist>>(_store.Where(b => ids.Contains(b.Id)).ToList());
        public Task<Blocklist?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_store.FirstOrDefault(b => b.Id == id));
        public Task AddAsync(Blocklist blocklist, CancellationToken ct = default) { _store.Add(blocklist); return Task.CompletedTask; }
        public Task UpdateAsync(Blocklist blocklist, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    
    public Task<int> AppendRulesAsync(Guid blocklistId, IReadOnlyList<BlockRule> rules, DateTime updatedAtUtc, CancellationToken ct = default) => Task.FromResult(rules.Count);
}

    private sealed class StubCategoryRepository : ICategoryRepository
    {
        public Task<IReadOnlyList<string>> GetDomainsAsync(string c, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<IReadOnlyList<string>> GetCategoryNamesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
