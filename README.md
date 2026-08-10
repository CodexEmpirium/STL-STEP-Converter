# STL STEP Converter

A small Windows WPF utility for converting between STL mesh files and STEP CAD files using the FreeCAD Python API.

The app provides two drag-and-drop panels, one for STL files and one for STEP/STP files. Opening or dropping a file starts conversion automatically and writes the converted file beside the source file with the matching extension.

## Requirements

- Windows
- FreeCAD installed
  - The app automatically looks for `FreeCADCmd.exe` and `FreeCAD.exe` in common FreeCAD install folders such as `C:\Program Files\FreeCAD*`.
  - If FreeCAD is installed elsewhere, use the `Browse` button to select `FreeCADCmd.exe`.
- .NET SDK 10.0 or newer for development
  - The standalone Release publish bundles the .NET runtime, so end users do not need to install .NET separately.

## Clone And Setup

```powershell
git clone <repo-url>
cd "STL-STEP Converter"
dotnet restore
```

Run the app during development:

```powershell
dotnet run
```

Build locally:

```powershell
dotnet build
```

Create a standalone Release build with the .NET runtime bundled:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The published executable is written to:

```text
bin\Release\net10.0-windows\win-x64\publish\StlStepConverter.exe
```

## Implemented Features

- STL to STEP conversion through FreeCAD's Python API.
- STEP/STP to STL conversion through FreeCAD's Python API.
- Automatic conversion after opening or dropping a file.
- Two drag-and-drop upload panels:
  - STL file panel
  - STEP/STP file panel
- Open buttons for each file type.
- File path and directory display for each selected or converted file.
- Automatic FreeCAD command discovery for common Windows install paths.
- Manual FreeCAD command selection with `Browse`.
- `View in FreeCAD` buttons for both file panels.
- Opposite-side view button stays disabled until conversion creates the matching file.
- Conversion progress bar driven by FreeCAD artifact progress events.
- Direction-specific progress text:
  - `Converting STL -> STEP done XX %`
  - `Converting STL <- STEP done XX %`
- Elapsed conversion timer.
- Live conversion logging from FreeCAD stdout/stderr.
- Output verification so success is only reported when the converted file exists and has content.
