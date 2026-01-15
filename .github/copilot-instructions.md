# SnipShot - AI Coding Instructions

## Project Overview
Windows screenshot app: **.NET 10**, **WinUI 3**, **Windows App SDK 1.8**, **Win2D** for graphics. **Spanish** comments/docs, **English** identifiers.

## Architecture

### Key Directories
- `Features/Capture/Modes/` - Capture modes extend `CaptureModeBase` and implement `ICaptureMode`
- `Features/Capture/Windows/ShadeOverlayWindow` - Unified overlay container loading modes dynamically
- `Features/Capture/Annotations/` - Drawing tools implementing `IAnnotationTool`
- `Helpers/UI/` - `ControlStateManager`, `ThemeHelper`, `ZoomManager`
- `Services/` - `CaptureOrchestratorService`, `DialogService`, `SettingsService`

### Capture Mode Pattern
All modes implement `ICaptureMode` (see `Features/Capture/Modes/Base/ICaptureMode.cs`):
```csharp
mode.Initialize(_backgroundBitmap, _virtualBounds, _availableWindows);
mode.Activate();  // ShadeOverlayWindow handles this sequence
```
Mode switching via `ModeChangeRequested` event—overlay stays open during internal transitions.

### Silent UI Updates
Use `ControlStateManager` to prevent event loops when setting control values:
```csharp
ControlStateManager.SetToggleSilently(toggle, value, handler);
ControlStateManager.SetSliderValueSilently(slider, value, handler);
```

### Annotation Tools Pattern
Drawing tools implement `IAnnotationTool` (see `Features/Capture/Annotations/Base/IAnnotationTool.cs`):
- `StartStroke(Point)` → `ContinueStroke(Point)` → `EndStroke()` lifecycle
- `AnnotationManager` coordinates tool selection and drawing state
- Tools: Pen, Highlighter, Shapes (Rectangle, Circle, Arrow, Line, Star)

## Build & Run
```powershell
dotnet build
dotnet run --project SnipShot/SnipShot.csproj
dotnet publish -c Release -r win-x64  # or win-arm64
```

## Conventions
- **Nullable enabled** - Always use `?` for optional references
- **Spanish XML docs**: `/// <summary>Descripción en español</summary>`
- **Colors**: Use `SnipShot.Helpers.Utils.ColorConverter` (avoid `System.Drawing.Color` conflicts)
- **Themes**: `ThemeHelper.ApplyTheme(element, "Light"|"Dark"|"Default")`
- **P/Invoke**: Native structs in `Models/NativeStructures.cs`, metrics in `Models/Constants.cs`

## Common Gotchas
- **XamlRoot**: `DialogService` requires `XamlRoot` after control loads—use lazy initialization
- **Bitmap ownership**: Call `SoftwareBitmap.Copy()` when completing captures to avoid disposal issues
- **Window detection**: Use `WindowEnumerationHelper.GetCaptureableWindows()` when switching to WindowCapture mode
- **Event handlers**: Use `ControlStateManager` in `Set*Preference` methods to avoid event loops
