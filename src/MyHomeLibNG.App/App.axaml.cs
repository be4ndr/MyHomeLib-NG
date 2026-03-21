using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MyHomeLibNG.Application;
using MyHomeLibNG.Infrastructure;
using MyHomeLibNG.App.ViewModels;
using AvaloniaApplication = Avalonia.Application;

namespace MyHomeLibNG.App;

public partial class App : AvaloniaApplication
{
    private ServiceProvider? _serviceProvider;
    public IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("Services are not ready yet.");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddMyHomeLibApplication();
        services.AddMyHomeLibInfrastructure("Data Source=MyHomeLibNG.db");
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.InitializeMyHomeLibInfrastructureAsync().GetAwaiter().GetResult();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
