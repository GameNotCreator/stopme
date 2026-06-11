using StopMe.Core.ValueObjects;

namespace StopMe.Service.Processes;

public interface IProcessProvider
{
    IEnumerable<RunningAppInfo> SnapshotRunning();
}
