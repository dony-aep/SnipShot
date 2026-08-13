# Changelog

Todos los cambios relevantes de este proyecto se documentaran en este archivo.

El formato esta basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/)
y este proyecto se adhiere a [Semantic Versioning](https://semver.org/lang/es/).

## [Unreleased]

## [1.2.0] - 2026-08-13

### Added
- Proyecto de pruebas SnipShot.Tests (MSTest) con 28 pruebas sobre la comprobacion de actualizaciones: comparacion de versiones, tolerancia a respuestas incompletas, localizacion del instalador y construccion de los mensajes de error HTTP
- Los controles interactivos ahora exponen AutomationId y, cuando son solo icono, un nombre accesible; esto habilita lectores de pantalla y pruebas de UI Automation
- Los botones cuyo tooltip cambia en ejecucion (modo de captura, delay, OCR) sincronizan tambien su nombre accesible
- Indicador (Debug) junto a la version en Ajustes y en el tooltip de la bandeja, para distinguir de un vistazo una compilacion de desarrollo de una de Release
- Abrir la carpeta de capturas con una imagen abierta la muestra ya seleccionada en el Explorador
- Variantes altform-unplated del icono en 16, 32, 48 y 256 px; antes solo existia la de 24
- Icono de ventana establecido con AppWindow.SetIcon, que Alt+Tab y el Administrador de tareas no toman del manifiesto
- CLAUDE.md con la guia del repositorio: comandos de build y empaquetado, arquitectura, convenciones de interoperabilidad nativa y matriz de iconos requerida

### Changed
- Todas las firmas P/Invoke se unificaron en Models/NativeMethods.cs: una sola declaracion por funcion nativa, sin duplicados repartidos por 13 archivos
- Los structs nativos compartidos (POINT, RECT, MONITORINFO, MSG, BITMAPINFO) tienen ahora una unica definicion en Models/NativeStructures.cs
- Las funciones con variantes ANSI/Unicode declaran EntryPoint explicito terminado en W
- UpdateService aplica un timeout de 15 segundos, configura la cabecera User-Agent una sola vez y tolera respuestas incompletas de la API de GitHub
- La interpretacion de la respuesta de GitHub se separo de la llamada HTTP en UpdateService.ParseReleaseResponse, para poder probarla sin red
- El analisis de trimming vuelve a estar activo sobre el codigo propio: se retiro SuppressTrimAnalysisWarnings y solo se silencia IL2104 de los ensamblados del Windows App SDK
- Los selectores de modo de captura y de temporizador pasan de menu a lista de seleccion con el elemento activo resaltado, y se abren superpuestos al boton alineando la opcion activa sobre el
- Los menus flotantes de los overlays rectangular, de ventana y de forma libre reciben el mismo tratamiento
- Las etiquetas de los modos se acortaron: Captura Rectangular y Captura de Ventana pasan a Rectangular y Ventana
- El area de la imagen aprovecha todo el contenedor, sin margenes reservados
- Durante el recorte hay mas holgura alrededor de la imagen (de 24 a 120 px) para poder moverla con comodidad
- Las transiciones de zoom son animadas en lugar de instantaneas
- El zoom minimo se calcula segun la imagen: no se puede alejar mas alla de verla completa, y el limite se aplica tambien al zoom con Ctrl+rueda
- Ctrl + arrastrar desplaza la imagen desde cualquier punto del visor, tambien fuera de ella y durante el recorte
- Al aplicar un recorte el zoom se ajusta a la imagen resultante; al cancelarlo vuelve el que habia antes de entrar
- El atajo de Ajustar a ventana pasa de Ctrl+Shift+0 a Ctrl+9, porque Windows intercepta Ctrl+Shift para cambiar de distribucion de teclado
- El enlace a la configuracion de Windows para liberar Print Screen apunta a la pagina donde Windows 11 25H2 movio la opcion, con la ruta anterior como respaldo
- El aviso de conflicto con la Herramienta de Recortes se revisa cada vez que se abre el panel de Ajustes, no solo al arrancar
- El arte original del logo deja de incluirse en el paquete MSIX, donde ocupaba 400 KB sin que Windows lo use
- Scripts/Generate-AppIcons.ps1 genera las cinco variantes unplated y usa la sintaxis de ImageMagick 7

### Fixed
- El bucle de mensajes de la bandeja y el subclassing de la ventana principal usaban las variantes ANSI de GetMessage, DispatchMessage, DefWindowProc, PostMessage y CallWindowProc sobre ventanas creadas como Unicode
- Tres metodos asincronos que no eran manejadores de eventos declaraban async void, con lo que una excepcion cerraba la aplicacion en lugar de propagarse
- El icono de la aplicacion no se dibujaba en la barra de tareas de los monitores con una escala distinta del 100%, por faltar las variantes unplated de ese tamaño
- Una seleccion rectangular menor de 25 px se descartaba en silencio: no aparecia la barra de herramientas y habia que repetirla sin ninguna pista del motivo
- Los atajos de acercar y alejar no respondian salvo que el foco estuviera dentro del panel del editor
- Acercar y alejar partian de un nivel de zoom obsoleto si antes se habia usado Ctrl+rueda
- El zoom crecia hacia la esquina superior izquierda en lugar de desde el centro de la vista
- Deshacer o rehacer un texto o un emoji en el overlay de captura dejaba su marco de manipulacion flotando sobre una anotacion que ya no existia
- Deshacer un movimiento o un redimensionado cambiaba los datos de la forma pero no la redibujaba
- Deshacer un borrado devolvia la anotacion encima de todas las demas en lugar de a su orden original
- Los tiradores del recorte se agrandaban o encogian con el zoom, justo cuando mas precision hace falta
- El tooltip del boton de descartar captura saltaba sobre el area de captura al pasar el raton por la barra
- Al agotarse el limite de la API de GitHub, la comprobacion de actualizaciones mostraba solo Forbidden; ahora explica que se alcanzo el limite e indica a que hora se restablece. Un 403 por permisos se sigue distinguiendo, porque lo que identifica al limite es que x-ratelimit-remaining venga a 0

### Removed
- Helpers/Capture/ScreenCaptureHelper.cs, sin ningun llamador, que duplicaba la conversion GDI a SoftwareBitmap con un intercambio de canales innecesario
- La fila que mostraba el porcentaje de zoom en el menu, que quedaba desactualizada al hacer zoom con la rueda

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

[Unreleased]: https://github.com/dony-aep/SnipShot/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/dony-aep/SnipShot/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/dony-aep/SnipShot/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/dony-aep/SnipShot/releases/tag/v1.0.0
