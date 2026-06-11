namespace StopMe.Service.Ipc;

public interface IUserContext
{
    Guid CurrentUserProfileId { get; }
}

// Phase 2 single-user assumption. Phase 3 (UI sign-in / sync) will swap in a context that
// resolves the user dynamically.
public sealed class DefaultUserContext : IUserContext
{
    public static readonly Guid DefaultId = new("11111111-1111-1111-1111-111111111111");

    public Guid CurrentUserProfileId { get; internal set; } = DefaultId;
}
