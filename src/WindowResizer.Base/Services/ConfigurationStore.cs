using WindowResizer.Base.Abstractions;
using WindowResizer.Configuration;

namespace WindowResizer.Base.Services;

public sealed class ConfigurationStore : IConfigurationStore
{
    public void Save()
    {
        ConfigFactory.Save();
    }
}
