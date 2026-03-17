using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MyHomeLibNG.Application;
using MyHomeLibNG.Infrastructure;
using AvaloniaApplication = Avalonia.Application;

namespace MyHomeLibNG.App;

public partial class App : AvaloniaApplication
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddMyHomeLibApplication();
        services.AddMyHomeLibInfrastructure("Data Source=MyHomeLibNG.db");
        _serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
