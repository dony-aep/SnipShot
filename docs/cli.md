# Comando `snipshot`

`snipshot` pide capturas a SnipShot desde la terminal, un script o un agente de IA, y devuelve la ruta del PNG guardado. La captura la hace la app instalada, con la misma configuración que usas desde la interfaz: borde, carpeta de autoguardado y «Ocultar al capturar».

## Requisitos

- SnipShot 1.3.0 o superior. El comando se instala con la app y queda disponible en cualquier terminal nueva; no hay que tocar el PATH.
- La opción **Permitir control externo** activada en Configuración > General. Viene desactivada: mientras lo esté, ningún proceso puede pedir capturas a la app.
- La terminal y SnipShot con el mismo nivel de elevación. Una terminal abierta como administrador no puede conectar con una app que no lo está.

Si SnipShot está cerrado, cualquier captura lo abre en la bandeja, sin mostrar la ventana, y espera hasta 15 segundos a que responda. `status` nunca abre la app.

## Comandos

```text
snipshot status
snipshot list-windows
snipshot capture fullscreen
snipshot capture window   --hwnd <h> | --title <texto> | --process <nombre> | --interactive
snipshot capture region   [--rect <x>,<y>,<ancho>,<alto>]
snipshot capture freeform
```

| Comando | Qué hace |
|---|---|
| `status` | Devuelve la versión de la app, su PID y la carpeta de autoguardado |
| `list-windows` | Lista las ventanas que se pueden capturar, con handle, título, proceso, PID, posición y tamaño |
| `capture fullscreen` | Captura todos los monitores |
| `capture window` | Captura una ventana, aunque esté tapada por otras. Las minimizadas no se pueden capturar |
| `capture region --rect` | Captura un rectángulo de pantalla sin interacción |
| `capture region` | Abre el overlay rectangular para que el usuario dibuje el área |
| `capture freeform` | Abre el overlay de forma libre para que el usuario trace el contorno |
| `capture window --interactive` | Abre el overlay de ventana para que el usuario haga clic en una |

### Elegir la ventana

Se usa exactamente uno de estos selectores:

| Selector | Cómo compara | Ejemplo |
|---|---|---|
| `--hwnd` | Handle exacto, en decimal (como lo da `list-windows`) o en hexadecimal con `0x` | `--hwnd 395674` |
| `--title` | Subcadena del título, sin distinguir mayúsculas | `--title "Bloc de notas"` |
| `--process` | Nombre del proceso completo, con o sin `.exe`, sin distinguir mayúsculas | `--process notepad` |
| `--interactive` | El usuario hace clic en la ventana en el overlay | |

Si el selector encaja con varias ventanas, no se captura nada: la respuesta trae las candidatas para repetir con `--hwnd`.

### Opciones de captura

| Opción | Efecto |
|---|---|
| `--out <archivo.png>` | Guarda en esa ruta. Acepta rutas relativas al directorio actual y debe terminar en `.png`. Sin ella, la captura va a la carpeta de autoguardado, esté activado el autoguardado o no |
| `--clipboard` | Copia también la imagen al portapapeles |
| `--delay <s>` | Espera de 0 a 30 segundos antes de capturar |
| `--timeout <s>` | Solo en capturas interactivas: segundos que se espera al usuario, de 5 a 600 (120 por defecto). Al agotarse se cierra el overlay |

Las coordenadas de `--rect` son de pantalla y en píxeles físicos. Un monitor situado a la izquierda o encima del principal tiene coordenadas negativas; las posiciones que devuelve `list-windows` usan el mismo sistema.

## Respuesta

Cada comando escribe una sola línea JSON en la salida estándar.

```json
{"version":1,"ok":true,"capture":{"path":"C:\\Users\\ana\\Pictures\\SnipShot\\SnipShot_2026-10-25_16-31-28.png","width":1920,"height":1020,"mode":"window","window":{"hwnd":131994,"title":"Proyecto - Visual Studio Code","process":"Code","processId":6968,"x":-1920,"y":0,"width":1920,"height":1020}}}
```

```json
{"version":1,"ok":false,"error":{"code":"ambiguous","message":"Coinciden 2 ventanas; repite la captura con --hwnd de la que quieras."},"windows":[...]}
```

- `capture` trae `path`, `width`, `height`, `mode` y, en el modo ventana, la ventana capturada.
- `list-windows` trae la lista en `windows`.
- `status` trae `appVersion`, `processId` y `autoSaveFolder`.
- Los acentos de los mensajes salen escapados (`\u00E1`); cualquier lector de JSON los convierte.

## Códigos de salida

| Código | `error.code` | Significado |
|---|---|---|
| 0 | | Éxito |
| 1 | `internal` | Error inesperado; suele ser una ruta de `--out` sin permiso de escritura |
| 2 | `invalid_request` | Argumentos mal formados |
| 2 | `unknown_command` | La app no reconoce el comando |
| 2 | `unsupported_version` | La instancia abierta usa otra versión del protocolo; reinicia SnipShot |
| 2 | `not_found` | Ninguna ventana coincide con el selector |
| 2 | `ambiguous` | Varias ventanas coinciden; vienen en `windows` |
| 3 | `cancelled` | El usuario canceló el overlay con Esc |
| 3 | `timeout` | Nadie completó el overlay antes de `--timeout` |
| 4 | `disabled` | SnipShot está abierto, pero el control externo está desactivado |
| 4 | `not_running` | SnipShot no está abierto, o se abrió y no respondió |
| 4 | `access_denied` | La terminal y la app tienen distinto nivel de elevación |
| 5 | `busy` | Ya hay otra captura en curso, normalmente un overlay abierto |

## Ejemplos

Capturar una ventana por el nombre de su programa y abrir la imagen:

```powershell
$r = snipshot capture window --process notepad | ConvertFrom-Json
if ($r.ok) { Invoke-Item $r.capture.path } else { $r.error.message }
```

Elegir la ventana a partir de la lista, para no depender del título:

```powershell
$ventana = (snipshot list-windows | ConvertFrom-Json).windows | Where-Object process -eq 'Code' | Select-Object -First 1
snipshot capture window --hwnd $ventana.hwnd --out capturas\editor.png
```

Capturar un rectángulo fijo del monitor izquierdo y dejarlo en el portapapeles:

```powershell
snipshot capture region --rect -1920,0,800,600 --clipboard
```

Dejar que el usuario dibuje el área, con un minuto de margen:

```powershell
snipshot capture region --timeout 60 --out recorte.png
```

## Uso desde un agente

El formato está pensado para que un agente lo use sin interpretar texto: una línea JSON y un código de salida por resultado. Lo que conviene que el agente sepa:

- «Esta ventana» no es la ventana en primer plano, que será la terminal o la app del propio agente. Si el usuario no la nombra, hay que mostrarle los títulos de `list-windows` y preguntar.
- `--hwnd`, tomado de `list-windows`, es el único selector que no puede encajar con otra ventana.
- Antes de lanzar una captura interactiva hay que avisar al usuario de lo que debe hacer, porque el comando se queda esperando hasta que dibuje, haga clic, cancele o se agote el tiempo.
- Ante `cancelled` o `timeout` no se debe reintentar sin preguntar.
- La imagen se puede abrir para comprobar o describir lo capturado.

## Cómo funciona

SnipShot abre un named pipe propio de la sesión de Windows mientras **Permitir control externo** está activado. El pipe solo acepta conexiones del mismo usuario y con el mismo nivel de elevación. El comando envía una petición JSON por él, la app la valida, captura y responde.

El protocolo lleva un número de versión. Si actualizas SnipShot a una versión con otro protocolo y la instancia antigua sigue abierta en la bandeja, la respuesta es `unsupported_version` con un mensaje que pide reiniciarla.

## Solución de problemas

| Respuesta | Qué hacer |
|---|---|
| `disabled` | Activa **Permitir control externo** en Configuración > General |
| `not_running` tras abrir la app | Comprueba que el control externo está activado; sin él, la app arranca pero no responde |
| `access_denied` | Abre la terminal sin elevar, o ejecuta SnipShot con el mismo nivel que la terminal |
| `not_found` con una ventana abierta | Restáurala si está minimizada; las minimizadas no aparecen en `list-windows` |
| `busy` | Termina o cancela el overlay abierto |
| La imagen incluye la ventana de SnipShot | Activa **Ocultar al capturar**; la captura respeta esa opción igual que el botón |
