# Changelog

Todos los cambios relevantes de este proyecto se documentaran en este archivo.

El formato esta basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/)
y este proyecto se adhiere a [Semantic Versioning](https://semver.org/lang/es/).

## [Unreleased]

## [1.1.0] - 2026-03-02

### Changed
- El guardado automatico de capturas ahora viene activado por defecto
- La opcion Iniciar con Windows ahora viene activada por defecto
- Cuando la app inicia por StartupTask se ejecuta en segundo plano y se oculta en la bandeja en lugar de abrirse en primer plano
- La captura iniciada desde system tray mantiene el flujo de previsualizacion dentro de la aplicacion
- El nombre de publisher mostrado en el manifiesto de la app se actualizo a dony-aep
- La seccion Acerca de en Ajustes ahora muestra la version actual y el año de forma dinamica

### Fixed
- Las capturas iniciadas por hotkey ahora usan un flujo estable y consistente de copiado al portapapeles con notificacion nativa
- Se corrigio la captura en modo ventana al cambiar de modo dentro del overlay para que procese el resultado segun el modo final seleccionado
- Al eliminar una captura o imagen cargada se elimina tambien el archivo asociado en disco cuando existe
- La URL de Sitio web en Ajustes se actualizo al nuevo dominio https://snipshotw3.vercel.app/

## [1.0.0] - 2026-01-14

### Added
- Lanzamiento inicial de SnipShot para Windows con flujos de captura, edicion y anotacion
- Verificacion manual de actualizaciones desde Configuracion consultando GitHub Releases
- Acceso directo desde Configuracion a la pagina de descarga/release mas reciente

### Changed
- Lectura automatica de la version de la app desde `Package.appxmanifest` para evitar mantener una version duplicada en el codigo

[Unreleased]: https://github.com/dony-aep/SnipShot/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/dony-aep/SnipShot/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/dony-aep/SnipShot/releases/tag/v1.0.0
