using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(MyHomeLibNG.App.HeadlessTests.HeadlessTestApp))]

namespace MyHomeLibNG.App.HeadlessTests;

public static class HeadlessTestApp
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .LogToTrace();

    private sealed class TestApplication : Avalonia.Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
        }
    }
}
