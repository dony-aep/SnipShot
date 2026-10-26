---
paths:
  - "SnipShot/Assets/**"
  - "Scripts/Generate-AppIcons.ps1"
---

# Iconos de la app

- La barra de tareas de Windows 11 usa las variantes `altform-unplated` de `Square44x44Logo` y elige el tamaño según la escala de **cada monitor**. En `Assets/` deben existir las cinco: `targetsize-16/24/32/48/256_altform-unplated`. Si falta la que pide un monitor, ese monitor deja el icono en blanco.
- Los iconos se regeneran con `Scripts/Generate-AppIcons.ps1` a partir de `Assets/logo-snipshot-app.png` (1080×1080 con alfa). Requiere ImageMagick en el PATH.
- Si añades un tamaño al script, añade **siempre** su variante `_altform-unplated`. La primera versión del script solo generaba la de 24, y el icono desaparecía en monitores que no estaban al 100 %.
- El arte original está excluido del paquete con `<Content Remove>` en el `.csproj`: el glob de `Content` mete en el MSIX todo lo que hay en `Assets/`, y son 400 KB que Windows no usa. Comprueba lo que entra con el comando `GetPackagingOutputs` de `CLAUDE.md`.
- El icono de la **ventana** (Alt+Tab, Administrador de tareas) no sale del manifiesto ni de la barra de título personalizada. Se establece con `AppWindow.SetIcon()` en `MainWindow`, apuntando al mismo `Assets/snipshot.ico` que usa la bandeja.
