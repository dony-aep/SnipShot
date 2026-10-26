---
paths:
  - "SnipShot.Cli/**"
  - "SnipShot/Services/ControlProtocol.cs"
  - "SnipShot/Services/ExternalControlService.cs"
  - "docs/cli.md"
---

# Control externo (comando `snipshot`)

## Cómo encaja

- `ExternalControlService` abre un named pipe por sesión de Windows (`ControlProtocol.GetPipeName`) solo mientras «Permitir control externo» está activada. La E/S va en segundo plano; cada petición se ejecuta en el hilo de UI con `DispatcherQueue`, porque las capturas abren overlays.
- El pipe usa `PipeOptions.CurrentUserOnly`: solo el mismo usuario y con el mismo nivel de elevación.
- `MainWindow.HandleExternalControlRequestAsync` atiende los comandos. Solo se acepta una captura a la vez (`_externalCaptureGate`); la siguiente recibe `busy`.

## Protocolo

- `ControlProtocol.cs` no puede depender de WinUI: lo compilan la app, el CLI (enlazado, sin referenciar `SnipShot.csproj`) y las pruebas. Se serializa con generación de código fuente (`ControlJsonContext`) para no romperse con el trimming.
- La app valida la petición (`TryParseRequest`): modos, rangos, rutas absolutas `.png`, selectores. El CLI (`CliArguments`) solo comprueba la sintaxis y resuelve las rutas relativas contra su directorio, porque el de la app es otro.
- Cada código de error tiene su código de salida en `GetExitCode`. Un código nuevo necesita su entrada ahí, su prueba y su fila en `docs/cli.md`.
- `ControlProtocol.Version` solo sube si el cambio deja de ser compatible.

## Añadir un comando u opción

1. `ControlProtocol.cs`: constante, campos de `ControlRequest`/`ControlResponse` y validación.
2. `CliArguments.cs`: sintaxis y texto de `Usage`.
3. `MainWindow.HandleExternalControlRequestAsync`: el manejador.
4. Pruebas en `ControlProtocolTests` y `CliArgumentsTests`, con dientes.
5. `docs/cli.md` y la skill `skills/capturing-screenshots-with-snipshot/SKILL.md`, que se publica con cada release.
6. Prueba con la app instalada (ver «Probar sobre la app instalada» en `CLAUDE.md`).

## Empaquetado del CLI

- El CLI es una segunda `<Application>` del manifiesto, oculta del menú Inicio (`AppListEntry="none"`) y con alias `snipshot.exe` de consola. Va aparte porque un alias de consola exige `desktop4:SupportsMultipleInstances="true"` en su `<Application>`, y no se quiere en la principal.
- Comparte el runtime autocontenido de la app. `<TrimmerRootAssembly Include="SnipShot.Cli" />` en `SnipShot.csproj` evita que el trimming de Release borre `SnipShot.Cli.dll` y las partes del runtime que usa (`System.Console`, `System.IO.Pipes`). Compruébalo ejecutando el CLI extraído del `.msix` Release, no solo compilando.
- `SnipShot.Cli.csproj` fija el RID por plataforma; sin él, el bundle falla al compilar arm64.
- Si la app está cerrada, el CLI la lanza con `SnipShot.exe --background` y `UseShellExecute = true`. Con `false` la app hereda la salida estándar del CLI, y quien la lee se queda esperando a que la app se cierre.
