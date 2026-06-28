using System.Reflection;

namespace MyHomeLibNG.App.ViewModels;

public sealed class SettingsViewModel
{
    public string AppName => "MyHomeLib NG";

    public string VersionText
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "Version unavailable" : $"Version {version}";
        }
    }
}
