using StopMe.Core.ValueObjects;
using StopMe.Service.Processes;

namespace StopMe.Service.Tests.Fakes;

internal sealed class FakeProcessProvider : IProcessProvider
{
    public List<RunningAppInfo> Snapshot { get; } = new();

    public IEnumerable<RunningAppInfo> SnapshotRunning() => Snapshot.ToArray();
}
