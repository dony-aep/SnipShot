# ============================================================
# SnipShot - App Icon Generator Script
# ============================================================
# Este script genera todos los iconos necesarios para una 
# aplicación WinUI 3 a partir de una imagen fuente.
#
# Requisitos:
#   - ImageMagick instalado (https://imagemagick.org/script/download.php)
#   - Asegúrate de marcar "Add to PATH" durante la instalación
#
# Uso:
#   .\Generate-AppIcons.ps1
# ============================================================

param(
    [string]$SourceImage = "..\SnipShot\Assets\logo-snipshot-app.png",
    [string]$OutputFolder = "..\SnipShot\Assets"
)

# Obtener rutas absolutas
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceImagePath = Join-Path $ScriptDir $SourceImage
$OutputFolderPath = Join-Path $ScriptDir $OutputFolder

# Verificar que ImageMagick está instalado
function Test-ImageMagick {
    try {
        $null = & magick --version 2>&1
        return $true
    }
    catch {
        return $false
    }
}

# Función para redimensionar imagen cuadrada
function New-SquareIcon {
    param(
        [string]$Source,
        [string]$Output,
        [int]$Size
    )
    
    Write-Host "  Generando: $Output ($Size x $Size)" -ForegroundColor Cyan
    & magick convert $Source -resize "${Size}x${Size}" -gravity center -background transparent -extent "${Size}x${Size}" $Output
}

# Función para crear imagen con padding (para tiles anchos y splash)
function New-PaddedIcon {
    param(
        [string]$Source,
        [string]$Output,
        [int]$Width,
        [int]$Height,
        [int]$IconSize = 0
    )
    
    if ($IconSize -eq 0) {
        # Usar el menor de width/height para el icono
        $IconSize = [Math]::Min($Width, $Height) * 0.6
    }
    
    Write-Host "  Generando: $Output ($Width x $Height, icono: $IconSize)" -ForegroundColor Cyan
    & magick convert $Source -resize "${IconSize}x${IconSize}" -gravity center -background transparent -extent "${Width}x${Height}" $Output
}

# ============================================================
# INICIO DEL SCRIPT
# ============================================================

Write-Host ""
Write-Host "============================================================" -ForegroundColor Yellow
Write-Host "  SnipShot - Generador de Iconos de Aplicación" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Yellow
Write-Host ""

# Verificar ImageMagick
Write-Host "Verificando ImageMagick..." -ForegroundColor White
if (-not (Test-ImageMagick)) {
    Write-Host ""
    Write-Host "ERROR: ImageMagick no está instalado o no está en el PATH." -ForegroundColor Red
    Write-Host ""
    Write-Host "Por favor, instala ImageMagick desde:" -ForegroundColor Yellow
    Write-Host "  https://imagemagick.org/script/download.php" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Durante la instalación, asegúrate de marcar:" -ForegroundColor Yellow
    Write-Host "  [x] Add application directory to your system path" -ForegroundColor Green
    Write-Host ""
    exit 1
}
Write-Host "  ImageMagick encontrado!" -ForegroundColor Green

# Verificar imagen fuente
Write-Host ""
Write-Host "Verificando imagen fuente..." -ForegroundColor White
if (-not (Test-Path $SourceImagePath)) {
    Write-Host "ERROR: No se encontró la imagen fuente:" -ForegroundColor Red
    Write-Host "  $SourceImagePath" -ForegroundColor Yellow
    exit 1
}
Write-Host "  Imagen encontrada: $SourceImagePath" -ForegroundColor Green

# Obtener dimensiones de la imagen fuente
$imageInfo = & magick identify -format "%wx%h" $SourceImagePath
Write-Host "  Dimensiones: $imageInfo" -ForegroundColor Green

# Crear carpeta de salida si no existe
if (-not (Test-Path $OutputFolderPath)) {
    New-Item -ItemType Directory -Path $OutputFolderPath -Force | Out-Null
}

Write-Host ""
Write-Host "Generando iconos en: $OutputFolderPath" -ForegroundColor White
Write-Host ""

# ============================================================
# Square44x44Logo - Icono de la aplicación (taskbar, Start menu)
# ============================================================
Write-Host "[1/7] Square44x44Logo (Taskbar/Start Menu)" -ForegroundColor Magenta

New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square44x44Logo.scale-100.png" -Size 44
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square44x44Logo.scale-125.png" -Size 55
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square44x44Logo.scale-150.png" -Size 66
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square44x44Logo.scale-200.png" -Size 88
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square44x44Logo.scale-400.png" -Size 176

# Target size variants (sin escala, tamaño exacto)
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square44x44Logo.targetsize-16.png" -Size 16
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square44x44Logo.targetsize-24.png" -Size 24
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square44x44Logo.targetsize-24_altform-unplated.png" -Size 24
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square44x44Logo.targetsize-32.png" -Size 32
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square44x44Logo.targetsize-48.png" -Size 48
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square44x44Logo.targetsize-256.png" -Size 256

Write-Host ""

# ============================================================
# Square150x150Logo - Tile mediano
# ============================================================
Write-Host "[2/7] Square150x150Logo (Medium Tile)" -ForegroundColor Magenta

New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square150x150Logo.scale-100.png" -Size 150
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square150x150Logo.scale-125.png" -Size 188
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square150x150Logo.scale-150.png" -Size 225
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square150x150Logo.scale-200.png" -Size 300
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square150x150Logo.scale-400.png" -Size 600

Write-Host ""

# ============================================================
# Wide310x150Logo - Tile ancho
# ============================================================
Write-Host "[3/7] Wide310x150Logo (Wide Tile)" -ForegroundColor Magenta

New-PaddedIcon -Source $SourceImagePath -Output "$OutputFolderPath\Wide310x150Logo.scale-100.png" -Width 310 -Height 150
New-PaddedIcon -Source $SourceImagePath -Output "$OutputFolderPath\Wide310x150Logo.scale-125.png" -Width 388 -Height 188
New-PaddedIcon -Source $SourceImagePath -Output "$OutputFolderPath\Wide310x150Logo.scale-150.png" -Width 465 -Height 225
New-PaddedIcon -Source $SourceImagePath -Output "$OutputFolderPath\Wide310x150Logo.scale-200.png" -Width 620 -Height 300
New-PaddedIcon -Source $SourceImagePath -Output "$OutputFolderPath\Wide310x150Logo.scale-400.png" -Width 1240 -Height 600

Write-Host ""

# ============================================================
# Square71x71Logo - Tile pequeño (Small Tile)
# ============================================================
Write-Host "[4/7] Square71x71Logo (Small Tile)" -ForegroundColor Magenta

New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square71x71Logo.scale-100.png" -Size 71
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square71x71Logo.scale-125.png" -Size 89
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square71x71Logo.scale-150.png" -Size 107
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square71x71Logo.scale-200.png" -Size 142
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\Square71x71Logo.scale-400.png" -Size 284

Write-Host ""

# ============================================================
# StoreLogo - Logo de la Microsoft Store
# ============================================================
Write-Host "[5/7] StoreLogo (Microsoft Store)" -ForegroundColor Magenta

New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\StoreLogo.scale-100.png" -Size 50
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\StoreLogo.scale-125.png" -Size 63
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\StoreLogo.scale-150.png" -Size 75
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\StoreLogo.scale-200.png" -Size 100
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\StoreLogo.scale-400.png" -Size 200

# Mantener el StoreLogo.png original (50x50) para compatibilidad
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\StoreLogo.png" -Size 50

Write-Host ""

# ============================================================
# SplashScreen - Pantalla de inicio
# ============================================================
Write-Host "[6/7] SplashScreen (Splash Screen)" -ForegroundColor Magenta

New-PaddedIcon -Source $SourceImagePath -Output "$OutputFolderPath\SplashScreen.scale-100.png" -Width 620 -Height 300 -IconSize 200
New-PaddedIcon -Source $SourceImagePath -Output "$OutputFolderPath\SplashScreen.scale-125.png" -Width 775 -Height 375 -IconSize 250
New-PaddedIcon -Source $SourceImagePath -Output "$OutputFolderPath\SplashScreen.scale-150.png" -Width 930 -Height 450 -IconSize 300
New-PaddedIcon -Source $SourceImagePath -Output "$OutputFolderPath\SplashScreen.scale-200.png" -Width 1240 -Height 600 -IconSize 400
New-PaddedIcon -Source $SourceImagePath -Output "$OutputFolderPath\SplashScreen.scale-400.png" -Width 2480 -Height 1200 -IconSize 800

Write-Host ""

# ============================================================
# LockScreenLogo - Logo en pantalla de bloqueo
# ============================================================
Write-Host "[7/7] LockScreenLogo (Lock Screen)" -ForegroundColor Magenta

New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\LockScreenLogo.scale-100.png" -Size 24
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\LockScreenLogo.scale-125.png" -Size 30
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\LockScreenLogo.scale-150.png" -Size 36
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\LockScreenLogo.scale-200.png" -Size 48
New-SquareIcon -Source $SourceImagePath -Output "$OutputFolderPath\LockScreenLogo.scale-400.png" -Size 96

Write-Host ""

# ============================================================
# Resumen
# ============================================================
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  ¡Iconos generados exitosamente!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Archivos generados en:" -ForegroundColor White
Write-Host "  $OutputFolderPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Próximos pasos:" -ForegroundColor Yellow
Write-Host "  1. Verifica los iconos generados en la carpeta Assets" -ForegroundColor White
Write-Host "  2. Reconstruye el proyecto en Visual Studio" -ForegroundColor White
Write-Host "  3. Los nuevos iconos se aplicarán automáticamente" -ForegroundColor White
Write-Host ""
