# CLAUDE.md

Este archivo guía a Claude Code (claude.ai/code) al trabajar con el código de este repositorio.

## Descripción del proyecto

SnipShot es una aplicación de escritorio de captura de pantalla para Windows (WinUI 3 + Windows App SDK 1.8, .NET 10, C#). Usa Win2D para renderizado 2D y MSIX de proyecto único (single-project MSIX, sin proyecto .wapproj separado). Es un proyecto local: se distribuye por sideload, **no** se publica en la Microsoft Store.

- Solución: `SnipShot.sln` — un único proyecto en `SnipShot/SnipShot.csproj`
- Requisito mínimo: Windows 11 22H2 (build 22621); plataformas: x64 y ARM64
- Idioma del proyecto: la documentación (README, CHANGELOG) y los comentarios están en español

## Comandos frecuentes

```powershell
# Compilar (desarrollo)
# El -p:Platform=x64 es obligatorio. Sin él, MSBuild resuelve AnyCPU y el empaquetado
# MSIX falla con "Packaged .NET applications with an app host exe cannot be
# ProcessorArchitecture neutral". Usar arm64 en esa máquina.
dotnet build SnipShot/SnipShot.csproj -p:Platform=x64

# Ejecutar
dotnet run --project SnipShot/SnipShot.csproj -p:Platform=x64

# Verificar que el trimming de Release no rompe nada (publica y ejecuta el análisis IL)
dotnet publish SnipShot/SnipShot.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true

# Generar el paquete MSIX bundle Release firmado para sideload
# (requiere MSBuild de Visual Studio; dotnet CLI NO genera el paquete MSIX)
# La ruta de MSBuild cambia segun donde este instalado VS, asi que se localiza con vswhere
# en vez de codificarla a mano.
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe"
& $msbuild `
    SnipShot.sln /restore /p:Configuration=Release /p:Platform=x64 `
    '/p:AppxBundlePlatforms=x64|arm64' /p:AppxBundle=Always `
    /p:UapAppxPackageBuildMode=SideloadOnly /p:GenerateAppxPackageOnBuild=true
```

El bundle firmado y su certificado `.cer` quedan en `SnipShot/AppPackages/SnipShot_<versión>_Test/`.

## Tests

```powershell
dotnet test SnipShot.Tests/SnipShot.Tests.csproj -p:Platform=x64
```

`SnipShot.Tests` (MSTest) cubre la lógica que no depende de UI ni de red.

**No referencia `SnipShot.csproj`**, y es a propósito: el Windows App SDK inyecta un *module initializer* en `SnipShot.dll` que arranca el Windows App Runtime al cargar el ensamblado, y fuera de un contexto empaquetado falla con `REGDB_E_CLASSNOTREG` antes de que se ejecute ninguna prueba. En su lugar el `.csproj` de tests enlaza con `<Compile Include>` las fuentes concretas que necesita.

Para añadir cobertura de otra clase, enlaza su archivo igual que `UpdateService.cs`. Solo funciona con clases sin dependencia de WinUI ni del Windows App SDK; hay unas 22 así (`CoordinateConverter`, `ColorInfo`, `FileHelper`, `FreeFormCaptureHelper`…). Si esa lista crece mucho, el paso natural es extraerlas a una librería `SnipShot.Core` sin dependencia de WinUI y sustituir los `<Compile Include>` por una `ProjectReference` normal.

Al probar lógica nueva, verifica que el test tenga dientes: rómpela a propósito y comprueba que el test falla. Un test que pasa con la lógica rota no cubre nada.

## Arquitectura

Todo el código vive en `SnipShot/`:

- `App.xaml.cs` / `MainWindow.xaml.cs` — arranque y ventana principal. Si la app inicia por la StartupTask (inicio con Windows), arranca oculta en la bandeja del sistema.
- `Features/Capture/` — flujos de captura organizados en `Modes` (pantalla completa, región, forma libre, ventana, selector de color), `Annotations`, `Toolbars` y `Windows` (overlays de selección).
- `Features/Editor/` — `ImageEditorControl`, el editor/anotador de la captura (formas, texto, resaltador, recorte, zoom, deshacer/rehacer).
- `Services/` — servicios singleton orquestadores: `CaptureOrchestratorService` (coordina el flujo completo de captura), `ScreenCaptureService`, `HotkeyService` (Ctrl+Shift+S / Print Screen), `NativeSystemTrayService`, `SettingsService`, `StartupService`, `UpdateService` (consulta GitHub Releases), `DialogService`.
- `Helpers/` — `Capture` (portapapeles, bordes, captura por GDI/Win2D), `UI` (managers de estado visual: zoom, autosave, delay, tema…), `Utils`, `WindowManagement` (enumeración y configuración de ventanas nativas).
- `Models/` — DTOs, constantes y estructuras nativas P/Invoke (`NativeStructures.cs`).
- `Shared/Controls/` — controles XAML reutilizables (toolbars y comunes).
- `Views/` — `SettingsView` (configuración).

### Interoperabilidad nativa

La bandeja del sistema usa `Shell_NotifyIcon` nativo por P/Invoke directamente (la librería H.NotifyIcon.WinUI se eliminó por alto consumo de CPU — no reintroducirla). Hay bastante P/Invoke para hotkeys globales, enumeración de ventanas y captura.

- **Todas** las firmas P/Invoke viven en `Models/NativeMethods.cs`, una sola declaración por función. No declarar `DllImport` en ningún otro archivo: los consumidores hacen `using static SnipShot.Models.NativeMethods;`.
- Los structs nativos compartidos (`POINT`, `RECT`, `MONITORINFO`, `MSG`, `BITMAPINFO`…) están en `Models/NativeStructures.cs`; los propios de la bandeja, en `Models/NativeSystemTrayStructures.cs`. No duplicarlos: dos definiciones del mismo struct es como se cuelan los fallos de alineación.
- Las funciones con variantes ANSI/Unicode llevan `EntryPoint` explícito terminado en `W`. Sin él, `DllImport` elige según el `CharSet` y el valor por defecto (Ansi) enlaza la variante `A` contra ventanas que la app crea como Unicode.
- Se usa `DllImport`, no `LibraryImport`, a propósito. Migrar obligaría a decidir a mano la variante A/W de 21 funciones sin poder verificarlo en ejecución, y para firmas blittable el trimming ya es seguro. Reconsiderar solo si se adopta Native AOT.

## Empaquetado y firma

- El paquete se firma con `SnipShot/SnipShot_TemporaryKey.pfx` (autofirmado, sin contraseña, sujeto `CN=doney` — debe coincidir con el `Publisher` de `Package.appxmanifest`). Si expira, generar uno nuevo desde Visual Studio (Package.appxmanifest → Empaquetado).
- Para instalar el bundle en una máquina nueva hay que importar antes el `.cer` generado junto al bundle en «Equipo local → Personas de confianza» (`Cert:\LocalMachine\TrustedPeople`, requiere admin) y luego instalar el `.msixbundle` con `Add-AppxPackage` o haciendo doble clic.
- La versión de la app se define en `Package.appxmanifest` (`Identity/Version`) y debe mantenerse alineada con el CHANGELOG.

### Iconos

La barra de tareas de Windows 11 usa las variantes **`altform-unplated`** de `Square44x44Logo`, y elige el tamaño según el factor de escala de **cada monitor**. En `Assets/` deben existir las cinco: `targetsize-16/24/32/48/256_altform-unplated`. Si falta la del tamaño que pide un monitor, ese monitor deja el icono en blanco mientras otro a distinta escala lo dibuja bien.

Los iconos se regeneran con `Scripts/Generate-AppIcons.ps1`, que parte de `Assets/logo-snipshot-app.png` (1080×1080 con alfa) y requiere ImageMagick en el PATH. Ese arte original está excluido del paquete con `<Content Remove>` en el `.csproj`: el glob de `Content` mete en el MSIX todo lo que haya en `Assets/`, y son 400 KB que Windows no usa.

Si se añaden tamaños al script, añadir **siempre** la variante `_altform-unplated` junto a la normal. La versión original del script solo generaba la de 24 y por eso el icono desaparecía en monitores que no estuvieran al 100%.

Para verificar qué entra realmente al paquete sin generar el MSIX completo:

```powershell
& $msbuild SnipShot\SnipShot.csproj -t:GetPackagingOutputs -p:Configuration=Debug -p:Platform=x64 `
    -nologo -v:q -getTargetResult:GetPackagingOutputs
```

El icono de la **ventana** (Alt+Tab, Administrador de tareas) es independiente de todo lo anterior: no sale del manifiesto ni de la Title Bar personalizada, y se establece con `AppWindow.SetIcon()` en `MainWindow.InitializeWindow`, apuntando al mismo `Assets/snipshot.ico` que usa la bandeja.

## Convenciones

- Release compila con `PublishTrimmed` y `PublishReadyToRun`: al añadir reflexión o librerías nuevas, verificar que no las rompa el trimming ejecutando el `dotnet publish` de arriba. El análisis IL está activo sobre el código propio y hoy sale limpio; solo se silencia `IL2104`, que emiten `WinRT.Runtime` y `Microsoft.Windows.SDK.NET`. **No** reintroducir `SuppressTrimAnalysisWarnings`: apagaría el único aviso que avisaría del problema.
- Los controles interactivos llevan `AutomationProperties.AutomationId` igual a su `x:Name`, y `AutomationProperties.Name` cuando son solo icono. Si un tooltip se actualiza en runtime, actualizar también el `AutomationProperties.Name` en el mismo sitio o el lector de pantalla anunciará el valor obsoleto.
- `CHANGELOG.md` sigue Keep a Changelog + Semantic Versioning, en español y sin tildes en ese archivo; los cambios nuevos van en `[Unreleased]`.
- Los commits y la documentación se escriben en español.
