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
| C# | 12 | Lenguaje de programación |

## Requisitos

- **Sistema operativo:** Windows 11 versión 22H2 (build 22621) o superior
- **Arquitecturas soportadas:** x64, ARM64
- **Para desarrollo:** 
  - .NET 10.0 SDK
  - Visual Studio 2022 (recomendado)
  - Windows App SDK 1.8

> ⚠️ **Nota:** Esta aplicación no es compatible con Windows 10 ni versiones anteriores de Windows 11.

## Compilación

```bash
# Clonar el repositorio
git clone https://github.com/dony-aep/SnipShot.git

# Navegar al directorio
cd SnipShot

# Restaurar dependencias
dotnet restore

# Compilar el proyecto
dotnet build

# Ejecutar la aplicación
dotnet run --project SnipShot/SnipShot.csproj
```

## Publicación

Para crear un paquete de distribución:

```bash
# Windows x64
dotnet publish -c Release -r win-x64

# Windows ARM64
dotnet publish -c Release -r win-arm64
```

## Licencia

Este proyecto está bajo la Licencia MIT. Consulta el archivo [LICENSE](LICENSE) para más detalles.

## Autor

**dony-aep**
