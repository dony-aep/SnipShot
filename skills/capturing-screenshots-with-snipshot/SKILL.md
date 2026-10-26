---
name: capturing-screenshots-with-snipshot
description: "Toma capturas con el comando snipshot de SnipShot: pantalla completa, una ventana o una parte de ella aunque esté tapada, una región por coordenadas, o un área que marca el usuario. Devuelve la ruta del PNG. Usar cuando el usuario pida una captura, un pantallazo o un screenshot, o cuando necesites ver una ventana de escritorio para comprobar un cambio. Takes screenshots with the SnipShot CLI."
---

# Capturas con SnipShot

`snipshot` se instala con SnipShot 1.3.0 o superior y queda en el PATH; `--rect` en capturas de ventana necesita la 1.4.0. Si el comando no existe, la app no está instalada: indícale al usuario que la descargue de https://github.com/dony-aep/SnipShot/releases. Cada comando escribe una sola línea JSON en la salida estándar y sale con un código que resume el resultado. Si SnipShot está cerrado, las capturas lo abren en la bandeja; `status` no.

## Comandos

```bash
snipshot capture fullscreen [--out <archivo.png>] [--clipboard] [--delay <segundos>]
snipshot list-windows
snipshot capture window --hwnd <hwnd> [--out <archivo.png>] [--clipboard] [--delay <segundos>]
snipshot capture window --process <nombre> | --title <texto>
snipshot capture window --hwnd <hwnd> --rect <x>,<y>,<ancho>,<alto>
snipshot capture region --rect <x>,<y>,<ancho>,<alto>
snipshot status
```

Interactivos (el usuario dibuja o hace clic en un overlay a pantalla completa):

```bash
snipshot capture region [--timeout <segundos>]
snipshot capture freeform [--timeout <segundos>]
snipshot capture window --interactive [--timeout <segundos>]
```

Todos admiten `--out`, `--clipboard` y `--delay`.

- Sin `--out`, el PNG va a la carpeta de autoguardado de SnipShot. `--out` acepta rutas relativas al directorio actual y debe terminar en `.png`.
- `--clipboard` deja además la imagen en el portapapeles. `--delay` espera de 0 a 30 segundos antes de capturar.
- La captura de ventana funciona aunque la ventana esté tapada. Las minimizadas no aparecen en `list-windows` y no se pueden capturar.
- `--rect` va en píxeles físicos. Con `capture window` las coordenadas parten de la esquina superior izquierda de la ventana. Con `capture region` son de pantalla: un monitor a la izquierda del principal tiene X negativa, y las posiciones de `list-windows` sirven de referencia.

## Capturas interactivas

Úsalas cuando el usuario quiera marcar él mismo qué capturar («el área que te señale», «un recorte») o cuando no se pueda deducir la ventana ni la región. Antes de lanzar el comando, dile qué tiene que hacer: dibujar el rectángulo y pulsar el botón de captura de la barra que aparece, trazar la forma libre o hacer clic en la ventana. El comando no termina hasta que lo haga, cancele con Esc o pasen `--timeout` segundos (120 por defecto).

## Capturar una ventana

1. Ejecuta `snipshot list-windows` y elige la ventana por `title` y `process`.
2. Captura con `--hwnd` de la elegida: es la única forma que no puede encajar con otra ventana.
3. Si el usuario nombra una aplicación y solo hay una ventana suya, `--process` (por ejemplo `notepad`, `chrome`, `Code`) sirve sin listar antes. Las apps de la Microsoft Store aparecen con el proceso `ApplicationFrameHost`, que comparten todas: elígelas por `title` o `hwnd`.

«Esta ventana» o «la ventana actual» no es la que está en primer plano: esa es la terminal o la app desde la que hablas con el usuario. Si no queda claro cuál quiere, muéstrale los títulos de `list-windows` y pregunta antes de capturar.

## Capturar una parte de una ventana

Para un panel, una barra lateral, un botón o el estado de una opción dentro de una ventana:

1. Captura la ventana entera con `--hwnd` y abre el PNG. Mide en esa imagen el rectángulo que quieres: sus píxeles son las coordenadas que espera `--rect`.
2. Captura solo esa parte con `snipshot capture window --hwnd <hwnd> --rect <x>,<y>,<ancho>,<alto>`. Funciona aunque la ventana esté tapada.

No traigas la ventana al frente (`AppActivate`, `SetForegroundWindow` o similares) ni uses `capture region --rect` para una parte de una ventana. `capture region` toma lo que hay en pantalla, que suele ser la terminal o la app desde la que hablas con el usuario, y cambiar la ventana activa le quita el foco al usuario. Deja `capture region --rect` para zonas de pantalla que no pertenecen a una sola ventana.

Si la respuesta es `invalid_request` con «rect solo se usa con el modo region», la versión instalada de SnipShot no admite `--rect` en ventanas. Entrega la captura de la ventana entera y señala la parte que pidió el usuario.

## Respuesta

```json
{"version":1,"ok":true,"capture":{"path":"C:\\Users\\u\\Pictures\\SnipShot\\SnipShot_2026-10-25_16-31-28.png","width":1920,"height":1020,"mode":"window","window":{"hwnd":131994,"title":"...","process":"msedge"}}}
```

Indica al usuario `capture.path` tal cual. Para comprobar o describir lo capturado, abre el PNG con la herramienta de lectura de archivos.

## Códigos de salida

| Código | `error.code` | Qué hacer |
|---|---|---|
| 0 | | Éxito |
| 1 | `internal` | Muestra `error.message`; suele ser una ruta de `--out` sin permiso de escritura |
| 2 | `ambiguous` | La respuesta trae las candidatas en `windows`; repite con `--hwnd` o pregunta cuál |
| 2 | `not_found` | Ninguna ventana coincide; ejecuta `list-windows` |
| 2 | `invalid_request` | Argumentos mal formados; corrige según `error.message` |
| 3 | `cancelled` | El usuario canceló el overlay; no repitas sin preguntarle |
| 3 | `timeout` | Nadie completó el overlay a tiempo; pregunta si quiere intentarlo otra vez |
| 4 | `disabled` | Pide al usuario que active «Permitir control externo» en SnipShot, Configuración > General |
| 4 | `not_running` | SnipShot no respondió; con la opción desactivada no llega a responder nunca |
| 4 | `access_denied` | La terminal y SnipShot tienen distinto nivel de elevación (una como administrador y otra no) |
| 5 | `busy` | Hay otra captura en curso (normalmente un overlay abierto); espera a que termine |
