using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using Qivisoft.BarcodeApp.Models;
using Qivisoft.BarcodeApp.Services;
using CommunityToolkit.Mvvm.Input;

namespace Qivisoft.BarcodeApp.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ImportService _importService = new();
    private static readonly ITextRasterizer TextRasterizer = new SkiaTextRasterizer();
    private readonly IAppSettingsStore _settingsStore;
    private readonly IZebraPrinterService _zebraPrinterService;
    private readonly ISystemQueuePrinterService _systemQueuePrinterService;
    private readonly List<PrinterProfileSettings> _printerProfiles = [];
    private bool _isApplyingSettings;
    private bool _isApplyingProfile;
    private bool _isPrintInProgress;

    private const int DefaultPrinterDpi = 203;
    private const string DefaultLabelWidthDotsText = "600";
    private const string DefaultBarcodeHeightDotsText = "110";
    private const string DefaultLabelHeightDotsText = "0";
    private const string DefaultPrinterPortText = "9100";
    private const string DefaultProfileName = "Domyslny";

    public MainWindowViewModel(
        IAppSettingsStore? settingsStore = null,
        IZebraPrinterService? zebraPrinterService = null,
        ISystemQueuePrinterService? systemQueuePrinterService = null)
    {
        _settingsStore = settingsStore ?? new FileAppSettingsStore();
        _zebraPrinterService = zebraPrinterService ?? new ZebraPrinterService();
        _systemQueuePrinterService = systemQueuePrinterService ?? new SystemQueuePrinterService();

        Rows.CollectionChanged += OnRowsCollectionChanged;

        AddRowCommand = new RelayCommand(AddRow);
        RemoveSelectedRowCommand = new RelayCommand(RemoveSelectedRow, () => Rows.Count > 0);
        ClearRowsCommand = new RelayCommand(ClearRows, () => Rows.Count > 0);
        ResetSettingsCommand = new RelayCommand(ResetSettingsToDefaults);
        AddProfileCommand = new RelayCommand(AddProfile);
        RemoveProfileCommand = new RelayCommand(RemoveCurrentProfile, () => PrinterProfileNames.Count > 1);
        Apply203DpiPresetCommand = new RelayCommand(() => ApplyDpiPreset(203));
        Apply300DpiPresetCommand = new RelayCommand(() => ApplyDpiPreset(300));
        Apply60x40PresetCommand = new RelayCommand(Apply60x40Preset);

        ApplySettings(_settingsStore.Load());
    }

    public ObservableCollection<ProductRowViewModel> Rows { get; } = [];

    public ObservableCollection<string> PrinterProfileNames { get; } = [];

    public IReadOnlyList<BarcodeSymbology> BarcodeTypeOptions { get; } =
    [
        BarcodeSymbology.Ean13,
        BarcodeSymbology.Ean8,
        BarcodeSymbology.UpcA,
        BarcodeSymbology.Code128
    ];

    public IReadOnlyList<string> LabelLayoutOptions { get; } =
    [
        "Klasyczny (nazwa + duży kod)",
        "Dwujęzyczny (PL / EN + SKU)"
    ];

    public LabelLayout SelectedLabelLayout
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            OnPropertyChanged(nameof(SelectedLabelLayoutIndex));
            OnPropertyChanged(nameof(IsMultilingualLayout));
            OnPropertyChanged(nameof(LabelLayoutHint));
            SaveSettings();
        }
    } = LabelLayout.Classic;

    /// <summary>Index-based binding for the layout ComboBox (0 = classic, 1 = multilingual).</summary>
    public int SelectedLabelLayoutIndex
    {
        get => (int)SelectedLabelLayout;
        set
        {
            if (value is >= 0 and <= 1)
                SelectedLabelLayout = (LabelLayout)value;
        }
    }

    /// <summary>Price/currency options are shown only for the multilingual layout.</summary>
    public bool IsMultilingualLayout => SelectedLabelLayout == LabelLayout.Multilingual;

    public IReadOnlyList<string> PriceCurrencyOptions { get; } = ["PLN (zł)", "EUR (€)"];

    public bool IncludePrice
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                SaveSettings();
        }
    }

    public PriceCurrency SelectedPriceCurrency
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            OnPropertyChanged(nameof(SelectedPriceCurrencyIndex));
            SaveSettings();
        }
    } = PriceCurrency.Pln;

    /// <summary>Index-based binding for the currency ComboBox (0 = PLN, 1 = EUR).</summary>
    public int SelectedPriceCurrencyIndex
    {
        get => (int)SelectedPriceCurrency;
        set
        {
            if (value is >= 0 and <= 1)
                SelectedPriceCurrency = (PriceCurrency)value;
        }
    }

    public string LabelLayoutHint =>
        SelectedLabelLayout == LabelLayout.Multilingual
            ? "Nazwa PL i EN (kolumna \"Nazwa EN\"), pogrubione SKU, opcjonalnie cena i mniejszy kod. Ustaw wys. etykiety, np. 320 dots = 40 mm."
            : "Do 2 linii opisu (nazwa, SKU, cena) nad dużym kodem kreskowym.";

    public ProductRowViewModel? SelectedRow
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) RemoveSelectedRowCommand.NotifyCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Zaimportuj plik CSV/XLS, aby rozpocząć.";

    public bool? IncludeProductName
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                SaveSettings();
        }
    } = true;

    public string SelectedPrinterProfileName
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            if (_isApplyingSettings)
                return;

            var selected = FindProfile(value);
            if (selected is null)
                return;

            ApplyProfileToEditor(selected);
            SaveSettings();
            RemoveProfileCommand.NotifyCanExecuteChanged();
        }
    } = DefaultProfileName;

    public BarcodeSymbology SelectedBarcodeType
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            if (_isApplyingSettings)
                return;

            ApplyBarcodeTypeToRows(value);
            SaveSettings();
        }
    } = BarcodeSymbology.Ean13;

    // ── Label size settings ──────────────────────────────────────────────────

    public int PrinterDpi
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(LabelWidthMm));
                OnPropertyChanged(nameof(LabelHeightMm));
                OnPropertyChanged(nameof(DpiHint));
                SaveSettings();
            }
        }
    } = DefaultPrinterDpi;

    public string LabelWidthDotsText
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(LabelWidthMm));
                SaveSettings();
            }
        }
    } = DefaultLabelWidthDotsText;

    public string BarcodeHeightDotsText
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                SaveSettings();
        }
    } = DefaultBarcodeHeightDotsText;

    public string PrinterHost
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                SaveSettings();
        }
    } = string.Empty;

    public string PrinterQueueName
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                SaveSettings();
        }
    } = string.Empty;

    public string PrinterPortText
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                SaveSettings();
        }
    } = DefaultPrinterPortText;

    public string LabelHeightDotsText
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(LabelHeightMm));
                SaveSettings();
            }
        }
    } = DefaultLabelHeightDotsText;

    public string LabelWidthMm =>
        int.TryParse(LabelWidthDotsText, out var w) && PrinterDpi > 0
            ? $"{w * 25.4 / PrinterDpi:F1} mm"
            : "—";

    public string LabelHeightMm =>
        int.TryParse(LabelHeightDotsText, out var h) && h > 0 && PrinterDpi > 0
            ? $"{h * 25.4 / PrinterDpi:F1} mm"
            : "auto";

    public string DpiHint =>
        PrinterDpi switch
        {
            203 => "203 dpi — 8 dots/mm (standard Zebra)",
            300 => "300 dpi — 12 dots/mm (wysoka jakość)",
            _ => $"{PrinterDpi} dpi"
        };

    public string BarcodeTypeHint =>
        SelectedBarcodeType switch
        {
            BarcodeSymbology.Ean13 => "EAN-13: 13 cyfr (w ZPL przekazywane 12 cyfr danych)",
            BarcodeSymbology.Ean8 => "EAN-8: 8 cyfr (w ZPL przekazywane 7 cyfr danych)",
            BarcodeSymbology.UpcA => "UPC-A: 12 cyfr (w ZPL przekazywane 11 cyfr danych)",
            BarcodeSymbology.Code128 => "Code 128: tekst/alnum, 1-48 znaków",
            _ => string.Empty
        };

    public int ValidRowsCount => Rows.Count(row => row.IsValid);

    public int InvalidRowsCount => Rows.Count - ValidRowsCount;

    public int TotalLabels => Rows
        .Where(row => row.IsValid)
        .Select(row => int.TryParse(row.QuantityText, out var qty) ? qty : 0)
        .Sum();

    public bool CanExport => ValidRowsCount > 0;

    public bool IsPrintInProgress
    {
        get => _isPrintInProgress;
        private set
        {
            if (!SetProperty(ref _isPrintInProgress, value))
                return;

            OnPropertyChanged(nameof(CanPrint));
        }
    }

    public bool CanPrint => CanExport && !IsPrintInProgress;

    public IRelayCommand AddRowCommand { get; }

    public IRelayCommand RemoveSelectedRowCommand { get; }

    public IRelayCommand ClearRowsCommand { get; }

    public IRelayCommand ResetSettingsCommand { get; }

    public IRelayCommand AddProfileCommand { get; }

    public IRelayCommand RemoveProfileCommand { get; }

    public IRelayCommand Apply203DpiPresetCommand { get; }

    public IRelayCommand Apply300DpiPresetCommand { get; }

    public IRelayCommand Apply60x40PresetCommand { get; }

    public void ImportFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Nie wybrano pliku wejściowego.";
            return;
        }

        try
        {
            var result = _importService.Import(path);
            Rows.Clear();

            foreach (var row in result.Rows)
            {
                var vmRow = ProductRowViewModel.FromInput(row);
                vmRow.BarcodeType = SelectedBarcodeType;
                AttachRowEvents(vmRow);
                Rows.Add(vmRow);
            }

            if (Rows.Count == 0)
            {
                StatusMessage = "Plik wczytany, ale nie rozpoznano żadnych wierszy danych.";
            }
            else
            {
                var warningPart = result.Warnings.Count > 0
                    ? $" Ostrzeżenia: {string.Join("; ", result.Warnings)}"
                    : string.Empty;
                StatusMessage = $"Zaimportowano {Rows.Count} wierszy z pliku {Path.GetFileName(path)}.{warningPart}";
            }

            NotifyStatsChanged();
            ClearRowsCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import nie powiódł się: {ex.Message}";
        }
    }

    public void ExportZplToPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Nie wybrano pliku wyjściowego.";
            return;
        }

        if (!TryBuildZplPayload(out var zpl, out var validProductsCount, out var validationMessage))
        {
            StatusMessage = validationMessage;
            return;
        }

        File.WriteAllText(path, zpl, new UTF8Encoding(false));
        StatusMessage = $"Wyeksportowano ZPL do {Path.GetFileName(path)} ({validProductsCount} produktów, {TotalLabels} etykiet).";
    }

    public async Task PrintZplToZebraAsync(CancellationToken cancellationToken = default)
    {
        if (IsPrintInProgress)
        {
            StatusMessage = "Drukowanie już trwa. Poczekaj na zakończenie bieżącego zadania.";
            return;
        }

        if (!TryBuildZplPayload(out var zpl, out var validProductsCount, out var validationMessage))
        {
            StatusMessage = validationMessage;
            return;
        }

        var queueName = PrinterQueueName.Trim();
        if (!string.IsNullOrWhiteSpace(queueName))
        {
            IsPrintInProgress = true;
            try
            {
                StatusMessage = $"Wysyłanie ZPL do kolejki '{queueName}'...";
                await _systemQueuePrinterService.SendRawAsync(queueName, zpl, cancellationToken);
                StatusMessage = $"Wysłano ZPL do drukarki USB/kolejki '{queueName}' ({validProductsCount} produktów, {TotalLabels} etykiet).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Druk ZPL nie powiódł się: {ex.Message}";
            }
            finally
            {
                IsPrintInProgress = false;
            }

            return;
        }

        var host = PrinterHost.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            StatusMessage = "Uzupełnij nazwę drukarki USB (kolejki) albo Host/IP drukarki ZEBRA.";
            return;
        }

        if (!int.TryParse(PrinterPortText, out var port) || port is < 1 or > 65535)
        {
            StatusMessage = "Port drukarki musi być liczbą w zakresie 1-65535.";
            return;
        }

        try
        {
            IsPrintInProgress = true;
            StatusMessage = $"Łączenie z drukarką ZEBRA {host}:{port} i wysyłanie ZPL...";
            await _zebraPrinterService.SendAsync(host, port, zpl, cancellationToken);
            StatusMessage = $"Wysłano ZPL na drukarkę ZEBRA ({host}:{port}) ({validProductsCount} produktów, {TotalLabels} etykiet).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Druk ZPL nie powiódł się: {ex.Message}";
        }
        finally
        {
            IsPrintInProgress = false;
        }
    }

    public void RemoveRow(ProductRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!Rows.Contains(row)) return;

        DetachRowEvents(row);
        Rows.Remove(row);
        if (ReferenceEquals(SelectedRow, row)) SelectedRow = Rows.LastOrDefault();

        ClearRowsCommand.NotifyCanExecuteChanged();
        NotifyStatsChanged();
    }

    private void AddRow()
    {
        var row = ProductRowViewModel.CreateEmpty();
        row.BarcodeType = SelectedBarcodeType;
        AttachRowEvents(row);
        Rows.Add(row);
        SelectedRow = row;
        ClearRowsCommand.NotifyCanExecuteChanged();
        NotifyStatsChanged();
    }

    private void RemoveSelectedRow()
    {
        var rowToRemove = SelectedRow ?? Rows.LastOrDefault();
        if (rowToRemove is null) return;

        DetachRowEvents(rowToRemove);
        Rows.Remove(rowToRemove);
        SelectedRow = Rows.LastOrDefault();
        ClearRowsCommand.NotifyCanExecuteChanged();
        NotifyStatsChanged();
    }

    private void ClearRows()
    {
        foreach (var row in Rows) DetachRowEvents(row);

        Rows.Clear();
        SelectedRow = null;
        StatusMessage = "Wiersze wyczyszczone.";
        ClearRowsCommand.NotifyCanExecuteChanged();
        NotifyStatsChanged();
    }

    private void ResetSettingsToDefaults()
    {
        ApplyProfile(new PrinterProfileSettings
        {
            Name = SelectedPrinterProfileName,
            BarcodeType = BarcodeSymbology.Ean13,
            PrinterDpi = DefaultPrinterDpi,
            LabelWidthDotsText = DefaultLabelWidthDotsText,
            BarcodeHeightDotsText = DefaultBarcodeHeightDotsText,
            LabelHeightDotsText = DefaultLabelHeightDotsText
        });

        IncludeProductName = true;

        SaveSettings();
        StatusMessage = $"Przywrócono domyślne ustawienia profilu '{SelectedPrinterProfileName}'.";
    }

    private void ApplySettings(AppUserSettings settings)
    {
        _isApplyingSettings = true;
        try
        {
            IncludeProductName = settings.IncludeProductName;
            SelectedLabelLayout = Enum.IsDefined(settings.LabelLayout) ? settings.LabelLayout : LabelLayout.Classic;
            IncludePrice = settings.IncludePrice;
            SelectedPriceCurrency = Enum.IsDefined(settings.PriceCurrency) ? settings.PriceCurrency : PriceCurrency.Pln;
            SelectedBarcodeType = settings.SelectedBarcodeType;

            _printerProfiles.Clear();
            var loadedProfiles = settings.PrinterProfiles.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();
            if (loadedProfiles.Count == 0)
            {
                // Migrate from legacy flat settings to profile-based configuration.
                loadedProfiles.Add(new PrinterProfileSettings
                {
                    Name = DefaultProfileName,
                    BarcodeType = settings.SelectedBarcodeType,
                    PrinterDpi = settings.PrinterDpi > 0 ? settings.PrinterDpi : DefaultPrinterDpi,
                    LabelWidthDotsText = string.IsNullOrWhiteSpace(settings.LabelWidthDotsText)
                        ? DefaultLabelWidthDotsText
                        : settings.LabelWidthDotsText,
                    BarcodeHeightDotsText = string.IsNullOrWhiteSpace(settings.BarcodeHeightDotsText)
                        ? DefaultBarcodeHeightDotsText
                        : settings.BarcodeHeightDotsText,
                    LabelHeightDotsText = string.IsNullOrWhiteSpace(settings.LabelHeightDotsText)
                        ? DefaultLabelHeightDotsText
                        : settings.LabelHeightDotsText
                });
            }

            _printerProfiles.AddRange(loadedProfiles.Select(CloneProfile));
            RefreshProfileNames();

            var activeProfileName = string.IsNullOrWhiteSpace(settings.ActivePrinterProfileName)
                ? _printerProfiles[0].Name
                : settings.ActivePrinterProfileName;
            var activeProfile = FindProfile(activeProfileName) ?? _printerProfiles[0];

            SelectedPrinterProfileName = activeProfile.Name;
            ApplyProfileToEditor(activeProfile);
            OnPropertyChanged(nameof(BarcodeTypeHint));
        }
        finally
        {
            _isApplyingSettings = false;
            RemoveProfileCommand.NotifyCanExecuteChanged();
        }
    }

    private void SaveSettings()
    {
        if (_isApplyingSettings || _isApplyingProfile)
            return;

        UpdateCurrentProfileFromEditor();

        _settingsStore.Save(new AppUserSettings
        {
            IncludeProductName = IncludeProductName ?? true,
            LabelLayout = SelectedLabelLayout,
            IncludePrice = IncludePrice,
            PriceCurrency = SelectedPriceCurrency,
            SelectedBarcodeType = SelectedBarcodeType,
            ActivePrinterProfileName = SelectedPrinterProfileName,
            PrinterProfiles = _printerProfiles.Select(CloneProfile).ToList(),
            // Legacy fields are still written for backward compatibility.
            PrinterDpi = PrinterDpi,
            LabelWidthDotsText = LabelWidthDotsText,
            BarcodeHeightDotsText = BarcodeHeightDotsText,
            LabelHeightDotsText = LabelHeightDotsText
        });
    }

    private void AddProfile()
    {
        UpdateCurrentProfileFromEditor();

        var uniqueName = BuildUniqueProfileName("Profil");
        var newProfile = new PrinterProfileSettings
        {
            Name = uniqueName,
            BarcodeType = SelectedBarcodeType,
            PrinterQueueName = PrinterQueueName,
            PrinterHost = PrinterHost,
            PrinterPort = int.TryParse(PrinterPortText, out var printerPort) && printerPort > 0 ? printerPort : 9100,
            PrinterDpi = PrinterDpi,
            LabelWidthDotsText = LabelWidthDotsText,
            BarcodeHeightDotsText = BarcodeHeightDotsText,
            LabelHeightDotsText = LabelHeightDotsText
        };

        _printerProfiles.Add(newProfile);
        RefreshProfileNames();
        SelectedPrinterProfileName = newProfile.Name;
        SaveSettings();
        StatusMessage = $"Dodano profil '{newProfile.Name}'.";
        RemoveProfileCommand.NotifyCanExecuteChanged();
    }

    private void RemoveCurrentProfile()
    {
        if (_printerProfiles.Count <= 1)
            return;

        var toRemove = FindProfile(SelectedPrinterProfileName);
        if (toRemove is null)
            return;

        var removedName = toRemove.Name;
        _printerProfiles.Remove(toRemove);
        RefreshProfileNames();
        SelectedPrinterProfileName = _printerProfiles[0].Name;
        ApplyProfileToEditor(_printerProfiles[0]);
        SaveSettings();
        StatusMessage = $"Usunięto profil '{removedName}'.";
        RemoveProfileCommand.NotifyCanExecuteChanged();
    }

    private void ApplyDpiPreset(int dpi)
    {
        if (dpi is not (203 or 300))
            return;

        PrinterDpi = dpi;
        LabelWidthDotsText = dpi == 203 ? "600" : "900";
        BarcodeHeightDotsText = dpi == 203 ? "110" : "165";
        LabelHeightDotsText = "0";

        SaveSettings();
        StatusMessage = $"Zastosowano preset {dpi} dpi dla profilu '{SelectedPrinterProfileName}'.";
    }

    /// <summary>
    /// Label 60 x 40 mm with the multilingual layout for the current printer resolution.
    /// </summary>
    private void Apply60x40Preset()
    {
        var dpi = PrinterDpi is 203 or 300 ? PrinterDpi : DefaultPrinterDpi;
        var dotsPerMm = dpi / 25.4;

        PrinterDpi = dpi;
        LabelWidthDotsText = ((int)Math.Round(60 * dotsPerMm)).ToString();
        LabelHeightDotsText = ((int)Math.Round(40 * dotsPerMm)).ToString();
        BarcodeHeightDotsText = ((int)Math.Round(14 * dotsPerMm)).ToString();
        SelectedLabelLayout = LabelLayout.Multilingual;

        SaveSettings();
        StatusMessage = $"Zastosowano preset 60 x 40 mm (układ dwujęzyczny, {dpi} dpi) dla profilu '{SelectedPrinterProfileName}'.";
    }

    private void RefreshProfileNames()
    {
        PrinterProfileNames.Clear();
        foreach (var profile in _printerProfiles)
            PrinterProfileNames.Add(profile.Name);
    }

    private PrinterProfileSettings? FindProfile(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return _printerProfiles.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.Ordinal));
    }

    private void ApplyProfileToEditor(PrinterProfileSettings profile)
    {
        _isApplyingProfile = true;
        try
        {
            ApplyProfile(profile);
        }
        finally
        {
            _isApplyingProfile = false;
        }
    }

    private void ApplyProfile(PrinterProfileSettings profile)
    {
        SelectedBarcodeType = profile.BarcodeType;
        PrinterQueueName = profile.PrinterQueueName;
        PrinterHost = profile.PrinterHost;
        PrinterPortText = profile.PrinterPort > 0 ? profile.PrinterPort.ToString() : DefaultPrinterPortText;
        PrinterDpi = profile.PrinterDpi > 0 ? profile.PrinterDpi : DefaultPrinterDpi;
        LabelWidthDotsText = string.IsNullOrWhiteSpace(profile.LabelWidthDotsText)
            ? DefaultLabelWidthDotsText
            : profile.LabelWidthDotsText;
        BarcodeHeightDotsText = string.IsNullOrWhiteSpace(profile.BarcodeHeightDotsText)
            ? DefaultBarcodeHeightDotsText
            : profile.BarcodeHeightDotsText;
        LabelHeightDotsText = string.IsNullOrWhiteSpace(profile.LabelHeightDotsText)
            ? DefaultLabelHeightDotsText
            : profile.LabelHeightDotsText;
    }

    private void UpdateCurrentProfileFromEditor()
    {
        var profile = FindProfile(SelectedPrinterProfileName);
        if (profile is null)
            return;

        profile.BarcodeType = SelectedBarcodeType;
        profile.PrinterQueueName = PrinterQueueName.Trim();
        profile.PrinterHost = PrinterHost.Trim();
        profile.PrinterPort = int.TryParse(PrinterPortText, out var printerPort) && printerPort > 0 ? printerPort : 9100;
        profile.PrinterDpi = PrinterDpi;
        profile.LabelWidthDotsText = LabelWidthDotsText;
        profile.BarcodeHeightDotsText = BarcodeHeightDotsText;
        profile.LabelHeightDotsText = LabelHeightDotsText;
    }

    private static PrinterProfileSettings CloneProfile(PrinterProfileSettings source)
    {
        return new PrinterProfileSettings
        {
            Name = source.Name,
            BarcodeType = source.BarcodeType,
            PrinterQueueName = source.PrinterQueueName,
            PrinterHost = source.PrinterHost,
            PrinterPort = source.PrinterPort,
            PrinterDpi = source.PrinterDpi,
            LabelWidthDotsText = source.LabelWidthDotsText,
            BarcodeHeightDotsText = source.BarcodeHeightDotsText,
            LabelHeightDotsText = source.LabelHeightDotsText
        };
    }

    private bool TryBuildZplPayload(out string zpl, out int validProductsCount, out string message)
    {
        zpl = string.Empty;
        validProductsCount = 0;

        var validData = CollectValidData();
        if (validData.Count == 0)
        {
            message = "Brak poprawnych wierszy do eksportu. Najpierw popraw błędy.";
            return false;
        }

        if (!TryBuildValidatedOptions(out var options, out message))
            return false;

        zpl = ZplBuilder.Build(validData, new ZplBuildOptions
        {
            BarcodeType = SelectedBarcodeType,
            IncludeProductName = IncludeProductName ?? true,
            LabelWidthDots = options.LabelWidthDots,
            BarcodeHeightDots = options.BarcodeHeightDots,
            LabelHeightDots = options.LabelHeightDots,
            Layout = SelectedLabelLayout,
            PrinterDpi = PrinterDpi,
            IncludePrice = IncludePrice,
            Currency = SelectedPriceCurrency,
            TextRasterizer = TextRasterizer
        });

        validProductsCount = validData.Count;
        message = string.Empty;
        return true;
    }

    private string BuildUniqueProfileName(string baseName)
    {
        var suffix = 1;
        var candidate = baseName;

        while (_printerProfiles.Any(p => string.Equals(p.Name, candidate, StringComparison.Ordinal)))
        {
            suffix++;
            candidate = $"{baseName} {suffix}";
        }

        return candidate;
    }

    private bool TryBuildValidatedOptions(out ValidatedBuildOptions options, out string message)
    {
        options = default;

        if (PrinterDpi is < 100 or > 600)
        {
            message = "DPI drukarki musi być w zakresie 100-600.";
            return false;
        }

        if (!int.TryParse(LabelWidthDotsText, out var width) || width is < 120 or > 2000)
        {
            message = "Szerokość etykiety (dots) musi być liczbą w zakresie 120-2000.";
            return false;
        }

        if (!int.TryParse(BarcodeHeightDotsText, out var barcodeHeight) || barcodeHeight is < 40 or > 800)
        {
            message = "Wysokość kodu (dots) musi być liczbą w zakresie 40-800.";
            return false;
        }

        if (!int.TryParse(LabelHeightDotsText, out var labelHeight) || labelHeight < 0)
        {
            message = "Wysokość etykiety (dots) musi być liczbą >= 0.";
            return false;
        }

        if (labelHeight > 0 && labelHeight < barcodeHeight + 40)
        {
            message = "Wysokość etykiety jest zbyt mała względem wysokości kodu. Zwiększ wysokość etykiety lub ustaw 0 (auto).";
            return false;
        }

        options = new ValidatedBuildOptions(width, barcodeHeight, labelHeight);
        message = string.Empty;
        return true;
    }

    private readonly record struct ValidatedBuildOptions(int LabelWidthDots, int BarcodeHeightDots, int LabelHeightDots);

    private void ApplyBarcodeTypeToRows(BarcodeSymbology type)
    {
        foreach (var row in Rows)
            row.BarcodeType = type;

        OnPropertyChanged(nameof(BarcodeTypeHint));
        NotifyStatsChanged();
    }

    private List<ValidProductData> CollectValidData()
    {
        var validData = new List<ValidProductData>();
        foreach (var row in Rows)
        {
            row.Validate();
            if (row.TryBuildValidData(out var data)) validData.Add(data);
        }

        NotifyStatsChanged();
        return validData;
    }

    private void OnRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (var item in e.NewItems.OfType<ProductRowViewModel>())
                AttachRowEvents(item);

        if (e.OldItems is not null)
            foreach (var item in e.OldItems.OfType<ProductRowViewModel>())
                DetachRowEvents(item);

        NotifyStatsChanged();
    }

    private void AttachRowEvents(ProductRowViewModel row)
    {
        row.RowChanged -= OnRowChanged;
        row.RowChanged += OnRowChanged;
    }

    private void DetachRowEvents(ProductRowViewModel row)
    {
        row.RowChanged -= OnRowChanged;
    }

    private void OnRowChanged(object? sender, EventArgs e)
    {
        NotifyStatsChanged();
    }

    private void NotifyStatsChanged()
    {
        OnPropertyChanged(nameof(ValidRowsCount));
        OnPropertyChanged(nameof(InvalidRowsCount));
        OnPropertyChanged(nameof(TotalLabels));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanPrint));
        RemoveSelectedRowCommand.NotifyCanExecuteChanged();
        ClearRowsCommand.NotifyCanExecuteChanged();
    }
}