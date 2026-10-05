using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Qivisoft.BarcodeApp.Models;
using Qivisoft.BarcodeApp.Services;
using Qivisoft.BarcodeApp.ViewModels;
using Qivisoft.BarcodeApp.Views;

[assembly: AvaloniaTestApplication(typeof(Qivisoft.BarcodeApp.SmokeTests.HeadlessTestApp))]

namespace Qivisoft.BarcodeApp.SmokeTests;

public static class HeadlessTestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>
/// Smoke tests that actually open the windows (headless), so XAML/runtime binding
/// problems that would prevent the app from starting are caught in CI.
/// </summary>
public sealed class MainWindowStartupTests
{
    [AvaloniaFact]
    public void MainWindow_Opens_WithDefaultSettings()
    {
        var window = new MainWindow { DataContext = new MainWindowViewModel(new MemoryStore(new AppUserSettings())) };

        window.Show();
        window.Close();
    }

    [AvaloniaFact]
    public void MainWindow_Opens_WithMultilingualLayoutAndPrice()
    {
        var settings = new AppUserSettings
        {
            LabelLayout = LabelLayout.Multilingual,
            IncludePrice = true,
            PriceCurrency = PriceCurrency.Eur
        };
        var window = new MainWindow { DataContext = new MainWindowViewModel(new MemoryStore(settings)) };

        window.Show();
        window.Close();
    }

    [AvaloniaFact]
    public void StickerPreviewWindow_Opens_ForMultilingualRow()
    {
        var row = new ProductRowViewModel
        {
            Ean = "5907053181876",
            Name = "OBROŻA S skóra zaplatana brąz",
            NameEn = "Braided leather collar S brown",
            Sku = "OZPASSBR",
            Price = "59,90",
            QuantityText = "1"
        };
        var window = new StickerPreviewWindow
        {
            DataContext = new StickerPreviewViewModel(row, true, BarcodeSymbology.Ean13,
                LabelLayout.Multilingual, true, PriceCurrency.Pln)
        };

        window.Show();
        window.Close();
    }

    private sealed class MemoryStore(AppUserSettings settings) : IAppSettingsStore
    {
        private AppUserSettings _settings = settings;

        public AppUserSettings Load() => _settings;

        public void Save(AppUserSettings settings) => _settings = settings;
    }
}
