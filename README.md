# SnipShot

Una aplicación de captura de pantalla moderna para Windows desarrollada con WinUI 3 y Windows App SDK.

## Características

### Modos de captura
- **Pantalla completa** - Captura todo el contenido visible de tus monitores
- **Región rectangular** - Selecciona un área específica de la pantalla
- **Forma libre** - Dibuja una selección personalizada con cualquier forma
- **Captura de ventana** - Selecciona una ventana específica para capturar
- **Selector de color** - Captura colores de cualquier punto de la pantalla

### Herramientas de anotación
- **Formas** - Rectángulos, círculos, líneas, flechas y estrellas
- **Bolígrafo** - Dibujo libre con colores y grosores personalizables
- **Resaltador** - Resalta áreas importantes con transparencia
- **Texto** - Añade texto con diferentes estilos, colores y resaltado
- **Emojis** - Inserta emojis directamente en tus capturas
- **Relleno** - Aplica relleno con color y opacidad a formas cerradas
- **Recorte** - Recorta la imagen después de capturar

### Funciones adicionales
- **Extracción de texto (OCR)** - Extrae texto de las imágenes capturadas
- **Búsqueda de imagen** - Busca imágenes similares en Google o Bing
- **Actualizaciones** - Verifica nuevas versiones desde Configuración y abre la descarga en GitHub Releases
- **Acerca de dinámico** - La sección Acerca de en Configuración muestra versión y año actualizados automáticamente
- **Guardado automático** - Guarda automáticamente las capturas en tu carpeta preferida (activado por defecto)
- **Delay configurable** - Programa capturas con retraso de 3, 5 o 10 segundos
- **Atajos de teclado** - Ctrl+Shift+S y Print Screen configurables con copia al portapapeles y notificación nativa
- **Bandeja del sistema** - Minimiza a la bandeja para acceso rápido
- **Inicio con Windows** - Opción activada por defecto; al arrancar por inicio de sesión se ejecuta en segundo plano
- **Borde personalizable** - Añade bordes con color y grosor configurable
- **Temas** - Soporte para tema claro, oscuro y automático del sistema
- **Zoom** - Acerca y aleja las capturas para edición precisa
- **Deshacer/Rehacer** - Historial completo de cambios en las anotaciones
- **Rotación de formas** - Rota formas y anotaciones libremente

## Tecnologías

| Tecnología | Versión | Descripción |
|------------|---------|-------------|
| .NET | 10.0 | Framework de desarrollo |
| Windows App SDK | 1.8 | SDK moderno para aplicaciones Windows |
| WinUI 3 | - | Framework de interfaz de usuario |
| Win2D | 1.3.2 | Motor de gráficos 2D de alto rendimiento |
| C# | 14 | Lenguaje de programación |

## Requisitos

- **Sistema operativo:** Windows 11 versión 22H2 (build 22621) o superior
- **Arquitecturas soportadas:** x64, ARM64
- **Para desarrollo:** 
  - .NET 10.0 SDK
  - Visual Studio 2026 (necesario para generar el paquete MSIX)
  - Windows App SDK 1.8

> ⚠️ **Nota:** Esta aplicación no es compatible con Windows 10 ni versiones anteriores de Windows 11.

## Instalación

Descarga el `.msixbundle` y el `.cer` de la [última release](https://github.com/dony-aep/SnipShot/releases/latest).

El paquete está firmado con un certificado autofirmado, así que la primera vez hay que
confiar en él. En PowerShell **como administrador**:

```powershell
Import-Certificate -FilePath .\SnipShot_1.2.0.0_x64_arm64.cer `
    -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Después ya se puede instalar el paquete con doble clic, o desde PowerShell:

```powershell
Add-AppxPackage .\SnipShot_1.2.0.0_x64_arm64.msixbundle
```

## Compilación

```powershell
# Clonar el repositorio
git clone https://github.com/dony-aep/SnipShot.git
cd SnipShot

# Compilar y ejecutar
# El -p:Platform=x64 es obligatorio: sin el, MSBuild resuelve AnyCPU y el
# empaquetado MSIX falla. En equipos ARM64 usar arm64 en su lugar.
dotnet build SnipShot/SnipShot.csproj -p:Platform=x64
dotnet run --project SnipShot/SnipShot.csproj -p:Platform=x64

# Pruebas
dotnet test SnipShot.Tests/SnipShot.Tests.csproj -p:Platform=x64
```

## Publicación

El paquete MSIX **no se genera con la CLI de dotnet**: hace falta MSBuild de Visual Studio.

```powershell
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe"

& $msbuild SnipShot.sln /restore /p:Configuration=Release /p:Platform=x64 `
    '/p:AppxBundlePlatforms=x64|arm64' /p:AppxBundle=Always `
    /p:UapAppxPackageBuildMode=SideloadOnly /p:GenerateAppxPackageOnBuild=true
```

El bundle firmado y su certificado quedan en `SnipShot/AppPackages/SnipShot_<versión>_Test/`.

## Licencia

Este proyecto está bajo la Licencia MIT. Consulta el archivo [LICENSE](LICENSE) para más detalles.

## Autor

**dony-aep**
