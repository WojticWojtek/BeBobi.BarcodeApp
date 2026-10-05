# QiviLabel

QiviLabel is a cross-platform desktop app for importing product data (CSV/XLS/XLSX), validating rows, and generating ZPL label output.

## Technical Stack

- Language: C# (.NET 10)
- UI: Avalonia 12 (desktop)
- Architecture: MVVM (CommunityToolkit.Mvvm)
- Data import:
  - CSV: CsvHelper
  - Excel: ClosedXML
- Tests: xUnit
- CI packaging: GitHub Actions (manual workflow)

## Repository Structure

- `BarcodeApp/` - application code (UI, ViewModels, Services, Models)
- `BarcodeApp.Tests/` - unit tests
- `DemoData/` - sample import data
- `.github/workflows/manual-build-package.yml` - manual packaging pipeline

## Core Features

- Import product rows from CSV/XLS/XLSX
- Row validation with user-visible validation messages
- Support for barcode symbologies:
  - EAN-13
  - EAN-8
  - UPC-A
  - Code 128
- ZPL export with configurable profile settings (DPI, width/height, barcode type)
- Persisted printer/profile settings
- Label layouts:
  - **Classic**: up to 2 description lines above a large barcode
  - **Multilingual**: PL / EN product names, bold SKU and a compact barcode
    (optional `Nazwa EN` import column, see `demodata/07_multilingual_60x40.csv`);
    long names are condensed automatically; "Preset 60 x 40 mm" sets the label size

## Local Development

### Prerequisites

- .NET SDK 10.x

### Build

```bash
dotnet build BarcodeApp.sln -c Debug
```

### Run tests

```bash
dotnet test BarcodeApp.sln --configuration Debug
```

### Local publish examples

```bash
# macOS Apple Silicon
dotnet publish BarcodeApp/BarcodeApp.csproj -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false

# macOS Intel
dotnet publish BarcodeApp/BarcodeApp.csproj -c Release -r osx-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false

# Windows x64
dotnet publish BarcodeApp/BarcodeApp.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false
```

## GitHub Actions: Manual Build Package

Workflow file:

- `.github/workflows/manual-build-package.yml`

### What it does

Manual workflow that:

1. Checks out the repository
2. Sets up .NET 10
3. Publishes selected runtime target(s)
4. Creates `QiviLabel/` output folder structure
5. Generates a ZIP package
6. Uploads ZIP as a workflow artifact

### Trigger type

- `workflow_dispatch` only (manual trigger)

### Input options

- `macos-silicon`
- `macos-intel`
- `windows-64`
- `all`

### Artifact output

- Artifact name: `QiviLabel-<target>`
- ZIP name: `QiviLabel-<target>.zip`
- Contains `QiviLabel/` folder with published app files and README

## Notes for Distribution

- Builds are self-contained, so client machines do not need a separate .NET runtime install.
- First launch may show OS security prompts:
  - macOS: Gatekeeper confirmation
  - Windows: SmartScreen warning for unsigned binaries

## Contact

- qivisoft@gmail.com
