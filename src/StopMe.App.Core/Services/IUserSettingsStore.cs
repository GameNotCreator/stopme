using StopMe.App.Core.Models;

namespace StopMe.App.Core.Services;

public interface IUserSettingsStore
{
    AppSettingsState Load();

    void Save(AppSettingsState state);
}
