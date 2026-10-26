# Changelog

Todos los cambios relevantes de este proyecto se documentarán en este archivo.

El formato está basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/)
y este proyecto se adhiere a [Semantic Versioning](https://semver.org/lang/es/).

## [Unreleased]

## [1.4.0] - 2026-10-26

### Added
- `snipshot capture window` admite `--rect` para capturar solo una parte de la ventana, con coordenadas relativas a ella. Funciona aunque la ventana esté tapada, así que un agente ya no tiene que traerla al frente ni quitarle el foco al usuario para capturar un panel o un botón
- Skill `capturing-screenshots-with-snipshot` para Claude Code, en `skills/` y adjunta como `.zip` a cada release: con ella, un agente sabe usar el comando sin explicaciones previas, desde elegir la ventana hasta actuar según el código de salida. Se instala copiándola en `%USERPROFILE%\.claude\skills\`

## [1.3.0] - 2026-10-25

### Added
- Comando `snipshot`, instalado con la app, para pedir capturas desde la terminal o desde un agente de IA sin tocar la interfaz: pantalla completa, una ventana concreta (por handle, título o proceso, aunque esté tapada), una región por coordenadas, o una región, forma libre o ventana que elige el usuario en el overlay. Devuelve la ruta del PNG en una línea JSON y un código de salida distinto para cada resultado; está documentado en `docs/cli.md`
- `snipshot list-windows` lista las ventanas que se pueden capturar con su título, proceso y posición, para elegir la correcta antes de capturar
- Opción «Permitir control externo» en Configuración > General, desactivada por defecto: sin ella ningún proceso puede pedir capturas a la app
- Si SnipShot está cerrado, el comando lo abre en la bandeja antes de capturar, sin mostrar la ventana
- 140 pruebas más, 168 en total, sobre el protocolo del comando, sus argumentos, la selección de ventanas y el recorte de capturas

### Changed
- La captura de ventana desde el overlay guarda exactamente lo que se veía al elegir, en lugar de cerrar el overlay y volver a capturar la pantalla
- El paquete se firma con un certificado nuevo, válido hasta 2031, y con sello de tiempo: los paquetes se podrán seguir instalando cuando caduque el certificado. Antes de actualizar desde una versión anterior hay que importar el `.cer` de esta versión

### Fixed
- Con la app en la bandeja o minimizada, una captura con atajo de teclado hacía aparecer la ventana; ahora solo se copia al portapapeles con su notificación, y «Nueva captura» sigue mostrando la app con la captura
- Al confirmar una captura rectangular, el overlay desaparecía, volvía y se cerraba en un instante, y al cerrarse cualquier overlay a veces se veía un fotograma negro
- En la captura de ventana se podía elegir la propia SnipShot cuando se iba a ocultar, y la imagen mostraba lo que había detrás de ella

## [1.2.0] - 2026-08-13

### Added
- Proyecto de pruebas SnipShot.Tests (MSTest) con 28 pruebas sobre la comprobación de actualizaciones: comparación de versiones, tolerancia a respuestas incompletas, localización del instalador y construcción de los mensajes de error HTTP
- Los controles interactivos ahora exponen AutomationId y, cuando son solo icono, un nombre accesible; esto habilita lectores de pantalla y pruebas de UI Automation
- Los botones cuyo tooltip cambia en ejecución (modo de captura, delay, OCR) sincronizan también su nombre accesible
- Indicador (Debug) junto a la versión en Ajustes y en el tooltip de la bandeja, para distinguir de un vistazo una compilación de desarrollo de una de Release
- Abrir la carpeta de capturas con una imagen abierta la muestra ya seleccionada en el Explorador
- Variantes altform-unplated del icono en 16, 32, 48 y 256 px; antes solo existía la de 24
- Icono de ventana establecido con AppWindow.SetIcon, que Alt+Tab y el Administrador de tareas no toman del manifiesto
- CLAUDE.md con la guía del repositorio: comandos de build y empaquetado, arquitectura, convenciones de interoperabilidad nativa y matriz de iconos requerida

### Changed
- Todas las firmas P/Invoke se unificaron en Models/NativeMethods.cs: una sola declaración por función nativa, sin duplicados repartidos por 13 archivos
- Los structs nativos compartidos (POINT, RECT, MONITORINFO, MSG, BITMAPINFO) tienen ahora una única definición en Models/NativeStructures.cs
- Las funciones con variantes ANSI/Unicode declaran EntryPoint explícito terminado en W
- UpdateService aplica un timeout de 15 segundos, configura la cabecera User-Agent una sola vez y tolera respuestas incompletas de la API de GitHub
- La interpretación de la respuesta de GitHub se separó de la llamada HTTP en UpdateService.ParseReleaseResponse, para poder probarla sin red
- El análisis de trimming vuelve a estar activo sobre el código propio: se retiró SuppressTrimAnalysisWarnings y solo se silencia IL2104 de los ensamblados del Windows App SDK
- Los selectores de modo de captura y de temporizador pasan de menú a lista de selección con el elemento activo resaltado, y se abren superpuestos al botón alineando la opción activa sobre él
- Los menús flotantes de los overlays rectangular, de ventana y de forma libre reciben el mismo tratamiento
- Las etiquetas de los modos se acortaron: Captura Rectangular y Captura de Ventana pasan a Rectangular y Ventana
- El área de la imagen aprovecha todo el contenedor, sin márgenes reservados
- Durante el recorte hay más holgura alrededor de la imagen (de 24 a 120 px) para poder moverla con comodidad
- Las transiciones de zoom son animadas en lugar de instantáneas
- El zoom mínimo se calcula según la imagen: no se puede alejar más allá de verla completa, y el límite se aplica también al zoom con Ctrl+rueda
- Ctrl + arrastrar desplaza la imagen desde cualquier punto del visor, también fuera de ella y durante el recorte
- Al aplicar un recorte el zoom se ajusta a la imagen resultante; al cancelarlo vuelve el que había antes de entrar
- El atajo de Ajustar a ventana pasa de Ctrl+Shift+0 a Ctrl+9, porque Windows intercepta Ctrl+Shift para cambiar de distribución de teclado
- El enlace a la configuración de Windows para liberar Print Screen apunta a la página donde Windows 11 25H2 movió la opción, con la ruta anterior como respaldo
- El aviso de conflicto con la Herramienta de Recortes se revisa cada vez que se abre el panel de Ajustes, no solo al arrancar
- El arte original del logo deja de incluirse en el paquete MSIX, donde ocupaba 400 KB sin que Windows lo use
- Scripts/Generate-AppIcons.ps1 genera las cinco variantes unplated y usa la sintaxis de ImageMagick 7

### Fixed
- El bucle de mensajes de la bandeja y el subclassing de la ventana principal usaban las variantes ANSI de GetMessage, DispatchMessage, DefWindowProc, PostMessage y CallWindowProc sobre ventanas creadas como Unicode
- Tres métodos asíncronos que no eran manejadores de eventos declaraban async void, con lo que una excepción cerraba la aplicación en lugar de propagarse
- El icono de la aplicación no se dibujaba en la barra de tareas de los monitores con una escala distinta del 100%, por faltar las variantes unplated de ese tamaño
- Una selección rectangular menor de 25 px se descartaba en silencio: no aparecía la barra de herramientas y había que repetirla sin ninguna pista del motivo
- Los atajos de acercar y alejar no respondían salvo que el foco estuviera dentro del panel del editor
- Acercar y alejar partían de un nivel de zoom obsoleto si antes se había usado Ctrl+rueda
- El zoom crecía hacia la esquina superior izquierda en lugar de desde el centro de la vista
- Deshacer o rehacer un texto o un emoji en el overlay de captura dejaba su marco de manipulación flotando sobre una anotación que ya no existía
- Deshacer un movimiento o un redimensionado cambiaba los datos de la forma pero no la redibujaba
- Deshacer un borrado devolvía la anotación encima de todas las demás en lugar de a su orden original
- Los tiradores del recorte se agrandaban o encogían con el zoom, justo cuando más precisión hace falta
- El tooltip del botón de descartar captura saltaba sobre el área de captura al pasar el ratón por la barra
- Al agotarse el límite de la API de GitHub, la comprobación de actualizaciones mostraba solo Forbidden; ahora explica que se alcanzó el límite e indica a qué hora se restablece. Un 403 por permisos se sigue distinguiendo, porque lo que identifica al límite es que x-ratelimit-remaining venga a 0

### Removed
- Helpers/Capture/ScreenCaptureHelper.cs, sin ningún llamador, que duplicaba la conversión GDI a SoftwareBitmap con un intercambio de canales innecesario
- La fila que mostraba el porcentaje de zoom en el menú, que quedaba desactualizada al hacer zoom con la rueda

## [1.1.0] - 2026-03-02

### Changed
- El guardado automático de capturas ahora viene activado por defecto
- La opción Iniciar con Windows ahora viene activada por defecto
- Cuando la app inicia por StartupTask se ejecuta en segundo plano y se oculta en la bandeja en lugar de abrirse en primer plano
- La captura iniciada desde system tray mantiene el flujo de previsualización dentro de la aplicación
- El nombre de publisher mostrado en el manifiesto de la app se actualizó a dony-aep
- La sección Acerca de en Ajustes ahora muestra la versión actual y el año de forma dinámica

### Fixed
- Las capturas iniciadas por hotkey ahora usan un flujo estable y consistente de copiado al portapapeles con notificación nativa
- Se corrigió la captura en modo ventana al cambiar de modo dentro del overlay para que procese el resultado según el modo final seleccionado
- Al eliminar una captura o imagen cargada se elimina también el archivo asociado en disco cuando existe
- La URL de Sitio web en Ajustes se actualizó al nuevo dominio https://snipshotw3.vercel.app/

## [1.0.0] - 2026-01-14

### Added
- Lanzamiento inicial de SnipShot para Windows con flujos de captura, edición y anotación
- Verificación manual de actualizaciones desde Configuración consultando GitHub Releases
- Acceso directo desde Configuración a la página de descarga/release más reciente

### Changed
- Lectura automática de la versión de la app desde `Package.appxmanifest` para evitar mantener una versión duplicada en el código

[Unreleased]: https://github.com/dony-aep/SnipShot/compare/v1.4.0...HEAD
[1.4.0]: https://github.com/dony-aep/SnipShot/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/dony-aep/SnipShot/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/dony-aep/SnipShot/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/dony-aep/SnipShot/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/dony-aep/SnipShot/releases/tag/v1.0.0
