namespace StopMe.Core.Enums;

public enum LockPolicy
{
    None = 0,
    Confirmation = 1,
    RandomText = 2,
    Password = 3,
    TimeLocked = 4,
    RestartRequired = 5,
}
