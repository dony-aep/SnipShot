# CLAUDE.md

Guía para trabajar en este repositorio. Las reglas de `.claude/rules/` añaden detalle cuando se tocan los iconos (`iconos.md`) o el control externo (`control-externo.md`).

## Proyecto

- SnipShot es una app de captura de pantalla para Windows: WinUI 3, Windows App SDK 1.8, .NET 10, C# y Win2D.
- Se empaqueta como MSIX de proyecto único (sin `.wapproj`) y se distribuye por sideload. **No** se publica en la Microsoft Store.
- Windows 11 22H2 (build 22621) como mínimo; plataformas x64 y ARM64.
- `SnipShot.sln` tiene tres proyectos:
  - `SnipShot/`: la app.
  - `SnipShot.Cli/`: el comando `snipshot`, que viaja dentro del mismo MSIX. Uso documentado en `docs/cli.md`.
  - `SnipShot.Tests/`: pruebas MSTest.
- Comentarios, documentación, CHANGELOG y commits van en español, con tildes y ortografía correcta.

## Comandos

```powershell
# Compilar y ejecutar. -p:Platform es obligatorio: sin él MSBuild resuelve AnyCPU y el
# empaquetado falla ("cannot be ProcessorArchitecture neutral"). En ARM64, arm64.
dotnet build SnipShot/SnipShot.csproj -p:Platform=x64
dotnet run --project SnipShot/SnipShot.csproj -p:Platform=x64

# Pruebas
dotnet test SnipShot.Tests/SnipShot.Tests.csproj -p:Platform=x64

# Comprobar que el trimming de Release no rompe nada (análisis IL incluido)
dotnet publish SnipShot/SnipShot.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true

# Bundle Release firmado para sideload. dotnet CLI no genera el MSIX: hace falta el MSBuild
# de Visual Studio, que se localiza con vswhere.
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe"
& $msbuild SnipShot.sln /restore /p:Configuration=Release /p:Platform=x64 `
    '/p:AppxBundlePlatforms=x64|arm64' /p:AppxBundle=Always `
    /p:UapAppxPackageBuildMode=SideloadOnly /p:GenerateAppxPackageOnBuild=true

# Qué entra en el paquete, sin generarlo
& $msbuild SnipShot\SnipShot.csproj -t:GetPackagingOutputs -p:Configuration=Debug -p:Platform=x64 `
    -nologo -v:q -getTargetResult:GetPackagingOutputs
```

El bundle y su `.cer` quedan en `SnipShot/AppPackages/SnipShot_<versión>_Test/`.

## Probar sobre la app instalada

El CLI, el pipe, la bandeja y el inicio con Windows necesitan la identidad del paquete: hay que probarlos con la app instalada.

- Para actualizar una instalación existente sin perder sus ajustes, genera un bundle con una revisión mayor (por ejemplo `1.3.0.1` en `Identity/Version`) e instálalo con `Add-AppxPackage -ForceApplicationShutdown`. Devuelve después el manifiesto a su versión.
- Una instalación hecha desde `.msixbundle` no se actualiza con un `.msix` suelto (error 0x80073CF3). Instalar la misma versión con otro contenido obliga a desinstalar, y eso borra los ajustes del usuario.
- Para manejar la interfaz y los overlays sin ratón, usa `winapp ui` (WinApp CLI 0.7+, skill `winui-ui-testing`). Los controles se localizan por su `AutomationId`.

## Pruebas

- `SnipShot.Tests` cubre la lógica sin UI ni red. **No** referencia `SnipShot.csproj`: el Windows App SDK inyecta en `SnipShot.dll` un *module initializer* que, fuera del paquete, falla con `REGDB_E_CLASSNOTREG` antes de ejecutar ninguna prueba.
- Las fuentes se enlazan con `<Compile Include>`, como `UpdateService.cs`, `ControlProtocol.cs`, `CliArguments.cs` y `PixelCompositor.cs`. Solo sirve para clases sin dependencia de WinUI ni del Windows App SDK. Si la lista crece mucho, extráelas a una librería `SnipShot.Core`.
- Cada prueba nueva tiene que tener dientes: rompe a propósito la lógica que cubre y comprueba que falla.

## Arquitectura

Todo el código de la app vive en `SnipShot/`:

- `App.xaml.cs`, `MainWindow.xaml.cs`: arranque y ventana principal. Con la StartupTask o con el argumento `--background` la app arranca oculta en la bandeja.
- `Features/Capture/`: flujos de captura en `Modes` (rectangular, forma libre, ventana, selector de color), `Annotations`, `Toolbars` y `Windows` (`ShadeOverlayWindow`, el overlay de selección).
- `Features/Editor/`: `ImageEditorControl`, el editor de la captura.
- `Services/`: `CaptureOrchestratorService` (coordina capturas y overlays), `ScreenCaptureService` (GDI y `PrintWindow`), `HotkeyService`, `NativeSystemTrayService`, `SettingsService`, `StartupService`, `UpdateService`, `DialogService`, y para el control externo `ExternalControlService` (servidor del pipe) y `ControlProtocol` (protocolo compartido con el CLI).
- `Helpers/`: `Capture` (portapapeles, bordes, recorte de píxeles), `UI` (visibilidad, zoom, autoguardado, delay, tema…), `Utils`, `WindowManagement` (enumeración de ventanas).
- `Models/`: DTOs, constantes y estructuras nativas.
- `Shared/Controls/`, `Views/`: controles reutilizables y `SettingsView`.

### Capturas y overlays

- Un overlay recibe una captura congelada de toda la pantalla. El resultado se recorta de esa imagen (`PixelCompositor`). Nunca se cambia el árbol visible para renderizarlo ni se vuelve a capturar la pantalla tras cerrar el overlay: las dos cosas producían el parpadeo de la 1.2.
- `ShadeOverlayWindow.CloseOverlay` oculta la ventana antes de desmontarla; si no, se ve un fotograma negro.
- `WindowVisibilityManager.ExecuteWithHiddenWindowAsync` solo oculta y restaura la ventana si estaba en pantalla. La visibilidad se pregunta a Win32 (`IsWindowVisible`, `IsIconic`), no a `AppWindow`, porque la bandeja oculta la ventana con `ShowWindow`.
- Una captura con atajo va al portapapeles con notificación y no muestra la ventana. «Nueva captura», desde el botón o la bandeja, sí la muestra con la captura en el editor.
- El overlay de ventana no ofrece la propia app cuando se va a ocultar: el recorte mostraría lo que hay detrás.

### Interoperabilidad nativa

- **Todas** las firmas P/Invoke viven en `Models/NativeMethods.cs`, una por función. No declares `DllImport` en otro archivo: los consumidores hacen `using static SnipShot.Models.NativeMethods;`.
- Los structs compartidos están en `Models/NativeStructures.cs`, y los de la bandeja, en `Models/NativeSystemTrayStructures.cs`. No los dupliques: dos definiciones del mismo struct es como se cuelan los fallos de alineación.
- Las funciones con variante ANSI/Unicode llevan `EntryPoint` terminado en `W`; con el `CharSet` por defecto se enlazaría la variante `A` contra ventanas Unicode.
- Se usa `DllImport` y no `LibraryImport` a propósito: migrar obligaría a decidir a mano la variante A/W de 21 funciones sin poder verificarlo. Reconsidéralo solo si se adopta Native AOT.
- La bandeja usa `Shell_NotifyIcon` directamente. No reintroduzcas H.NotifyIcon.WinUI, que se quitó por su consumo de CPU.

## Empaquetado y firma

- El paquete se firma con un certificado autofirmado de sujeto `CN=doney`, válido hasta el 2031-10-25. El sujeto debe coincidir con el `Publisher` de `Package.appxmanifest`: de él depende la identidad del paquete, así que un certificado nuevo con el mismo sujeto actualiza la app instalada sin desinstalarla.
- `SnipShot.csproj` lo busca por huella (`PackageCertificateThumbprint`) en `Cert:\CurrentUser\My`. No hay `.pfx` en el proyecto y `*.pfx` está en `.gitignore`: el repositorio es público.
- La copia de seguridad con contraseña está en `..\SnipShot-firma`, fuera del repositorio. La contraseña está a su lado en un `.clixml` cifrado con DPAPI, que solo descifra el usuario de Windows que lo creó. En otra máquina, importa el `.pfx` en `Cert:\CurrentUser\My` antes de generar el bundle.
- Las firmas llevan sello de tiempo (`AppxPackageSigningTimestampServerUrl`, DigiCert), así que los paquetes se siguen pudiendo instalar después de que caduque el certificado. Generar el bundle Release exige conexión a internet.
- Al renovar: mismo sujeto, actualiza la huella en el `.csproj` y publica el `.cer` nuevo con la release, porque cada máquina tiene que volver a confiar en él.
- En una máquina nueva hay que importar el `.cer` en `Cert:\LocalMachine\TrustedPeople` (como administrador) antes de instalar el bundle.
- La versión de la app está en `Package.appxmanifest` (`Identity/Version`) y debe coincidir con la última entrada del CHANGELOG.

## Convenciones

- Release compila con `PublishTrimmed` y `PublishReadyToRun`. Si añades reflexión o una librería, ejecuta el `dotnet publish` de arriba. El análisis IL está activo y sale limpio; solo se silencia `IL2104` (de `WinRT.Runtime` y `Microsoft.Windows.SDK.NET`). **No** reintroduzcas `SuppressTrimAnalysisWarnings`.
- Cada control interactivo lleva `AutomationProperties.AutomationId` igual a su `x:Name`, y `AutomationProperties.Name` si solo tiene icono. Si un tooltip cambia en ejecución, actualiza el `AutomationProperties.Name` en el mismo sitio.
- Para fijar el valor de un control desde código (por ejemplo en los `Set*Preference` de `SettingsView`), usa `ControlStateManager.SetToggleSilently` o `SetSliderValueSilently`; si no, se dispara su manejador y se crea un bucle de eventos.
- Para colores usa `SnipShot.Helpers.Utils.ColorConverter`, no `System.Drawing.Color`, que choca con los tipos de WinUI.
- `DialogService` necesita el `XamlRoot` de un control ya cargado: créalo de forma diferida, como la propiedad `DialogService` de `MainWindow`.
- Al completar una captura desde un overlay, entrega una copia del bitmap (`SoftwareBitmap.Copy`): el fondo original se libera al cerrarse el overlay.
- `CHANGELOG.md` sigue Keep a Changelog y Semantic Versioning, en español con tildes. Lo nuevo va en `[Unreleased]`.
- Si cambias un comando, una opción o un código de salida del CLI, actualiza `docs/cli.md` en el mismo commit.
