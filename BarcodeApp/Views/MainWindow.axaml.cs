using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Qivisoft.BarcodeApp.ViewModels;

namespace Qivisoft.BarcodeApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private async void ImportFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider is null) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Wybierz plik źródłowy CSV/XLS",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Pliki wejściowe")
                {
                    Patterns = ["*.csv", "*.xls", "*.xlsx"]
                }
            ]
        });

        var localPath = files.FirstOrDefault()?.TryGetLocalPath();
        ViewModel?.ImportFromPath(localPath);
    }

    private async void ExportZplButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider is null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Zapisz wygenerowany plik ZPL",
            SuggestedFileName = "etykiety.zpl",
            FileTypeChoices =
            [
                new FilePickerFileType("Pliki ZPL")
                {
                    Patterns = ["*.zpl"]
                }
            ]
        });

        var localPath = file?.TryGetLocalPath();
        ViewModel?.ExportZplToPath(localPath);
    }

    private async void PrintZplButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;

        await ViewModel.PrintZplToZebraAsync();
    }

    private void RemoveRowButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ProductRowViewModel row }) return;

        ViewModel?.RemoveRow(row);
    }

    private async void PreviewRowButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ProductRowViewModel row })
            return;

        var includeName = ViewModel?.IncludeProductName ?? true;
        var barcodeType = ViewModel?.SelectedBarcodeType ?? row.BarcodeType;

        var dialog = new StickerPreviewWindow
        {
            DataContext = new StickerPreviewViewModel(row, includeName, barcodeType,
                ViewModel?.SelectedLabelLayout ?? Models.LabelLayout.Classic)
        };

        await dialog.ShowDialog(this);
    }

    private async void ShowHelpButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Jak korzystać z aplikacji",
            Width = 700,
            Height = 500,
            MinWidth = 600,
            MinHeight = 420,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var description = new TextBlock
        {
            Text = "Ta aplikacja służy do importu produktów z CSV/XLS, edycji danych i generowania etykiet ZPL.",
            TextWrapping = TextWrapping.Wrap
        };

        var steps = new TextBlock
        {
            Text =
                "1. Kliknij \"Importuj CSV/XLS\" i wybierz plik.\n" +
                "2. Sprawdź kolumny EAN, nazwa produktu i ilość.\n" +
                "3. Popraw błędne wiersze (sekcja \"Walidacja\" pokaże problem).\n" +
                "4. W razie potrzeby dodawaj lub usuwaj wiersze.\n" +
                "5. Kliknij \"Eksportuj do pliku ZPL\" aby zapisać plik etykiet.\n" +
                "6. Aby drukować bezpośrednio: dla USB wpisz nazwę kolejki drukarki, a dla sieci wpisz host/IP i port ZEBRA. Następnie kliknij \"Drukuj ZPL na ZEBRA\".",
            TextWrapping = TextWrapping.Wrap
        };

        var tips = new TextBlock
        {
            Text =
                "Wskazówki:\n" +
                "- Poprawny EAN musi mieć 13 cyfr i prawidłową sumę kontrolną.\n" +
                "- Ilość musi być dodatnią liczbą całkowitą.\n" +
                "- Przełącznik nazwy produktu decyduje, czy nazwa pojawi się na etykiecie.\n" +
                "- Układ \"Dwujęzyczny\" (Ustawienia etykiety) drukuje nazwę PL, EN, SKU i mniejszy kod. " +
                "Dodaj w pliku kolumnę \"Nazwa EN\" (pusta jest pomijana).\n" +
                "- Etykieta 60 x 40 mm: kliknij \"Preset 60 x 40 mm\" w ustawieniach.",
            TextWrapping = TextWrapping.Wrap
        };

        var closeButton = new Button
        {
            Content = "Zamknij",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 100
        };
        closeButton.Click += (_, _) => dialog.Close();

        dialog.Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    description,
                    steps,
                    tips,
                    closeButton
                }
            }
        };

        await dialog.ShowDialog(this);
    }
}