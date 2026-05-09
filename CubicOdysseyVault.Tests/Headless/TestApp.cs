using Avalonia;
using Avalonia.Headless;
using CubicOdysseyVault.UI;

[assembly: AvaloniaTestApplication(typeof(CubicOdysseyVault.Tests.Headless.TestApp))]

namespace CubicOdysseyVault.Tests.Headless;

// Reuses the production App so we get the real ResourceDictionaries / Styles
// (DarkTheme, Components, FluentTheme) — without that, dialogs render without
// the cards/typography/brushes the layout assumes and measurements diverge
// from what users see.
public sealed class TestApp : App
{
    public override void OnFrameworkInitializationCompleted()
    {
        // Intentionally skip the production base call: it would create a
        // MainWindow on the desktop lifetime, which we don't want under the
        // headless test session.
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true,
            });
}
