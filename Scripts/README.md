# Scripts de SnipShot

Esta carpeta contiene scripts de utilidad para el proyecto SnipShot.

## Generate-AppIcons.ps1

Script para generar todos los iconos necesarios de la aplicación a partir de una imagen fuente.

### Requisitos

1. **ImageMagick** - Herramienta de procesamiento de imágenes
   - Descargar: https://imagemagick.org/script/download.php
   - Durante la instalación, **marcar la opción**: `Add application directory to your system path`

### Uso

1. Abre PowerShell en la carpeta `Scripts`
2. Ejecuta el script:

```powershell
.\Generate-AppIcons.ps1
```

### Parámetros opcionales

```powershell
# Especificar imagen fuente diferente
.\Generate-AppIcons.ps1 -SourceImage "..\SnipShot\Assets\mi-logo.png"

# Especificar carpeta de salida diferente
.\Generate-AppIcons.ps1 -OutputFolder "..\SnipShot\Assets\NewIcons"
```

### Iconos generados

El script genera los siguientes iconos con múltiples escalas:

| Tipo | Escalas | Uso |
|------|---------|-----|
| `Square44x44Logo` | 100%, 125%, 150%, 200%, 400% + targetsize variants | Taskbar, Start Menu |
| `Square150x150Logo` | 100%, 125%, 150%, 200%, 400% | Medium Tile |
| `Wide310x150Logo` | 100%, 125%, 150%, 200%, 400% | Wide Tile |
| `Square71x71Logo` | 100%, 125%, 150%, 200%, 400% | Small Tile |
| `StoreLogo` | 100%, 125%, 150%, 200%, 400% | Microsoft Store |
| `SplashScreen` | 100%, 125%, 150%, 200%, 400% | Splash Screen |
| `LockScreenLogo` | 100%, 125%, 150%, 200%, 400% | Lock Screen |

### Notas

- La imagen fuente debe ser cuadrada (1:1) con resolución mínima de 400x400 (recomendado: 1024x1024)
- Los iconos se generan con fondo transparente
- Los tiles anchos y splash screen centran el logo con padding automático
