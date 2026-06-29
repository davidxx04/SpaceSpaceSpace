# ART PLAN: Estética neón arcade / CRT para botones y UI

## Dirección visual

- **Paleta:** negro casi puro `#0a0b10` de base; neón **cian** `#3ce8ff` y **magenta** `#ff3ca0`
  como acentos primarios; ámbar `#ffb000` para avisos.
- **Fuente:** pixel/bitmap vía TMP (*Press Start 2P* o *m5x7*), importada como TMP Font Asset.
- **Forma botón:** marco 9-slice biselado fino (coherente con el HUD), esquinas con corte sci-fi.
- **Estados (cabina = joystick, no ratón):** Normal / Highlighted / **Selected** / Pressed / Disabled.
  El estado **Selected** es el más visible; diseñado primero.
- **Juice:** pulso lento de neón, ignición al seleccionar, flash + hundido + scale-punch al pulsar.

---

## Componentes a construir

### 1. Shader neón — `Assets/_Project/Art/Shaders/UINeon.shader`

Shader compatible con Canvas UI (basado en UI/Default + bloque stencil para máscaras).
Props animables vía `_Time`:

| Propiedad | Efecto |
|---|---|
| `_GlowColor` / `_GlowIntensity` | Halo de borde pulsante (falso rim glow en canal alfa del sprite) |
| `_ScanlineCount` / `_ScanlineStrength` | Líneas CRT horizontales |
| `_ChromaOffset` | Separación RGB sutil en bordes (aberración cromática) |
| `_PulseSpeed` | Latido del neón (oscilación de `_GlowIntensity`) |

`_GlowIntensity` es el único dial que varía por estado (lo anima `NeonButton`).

### 2. Sprites procedurales — `Assets/_Project/Scripts/Editor/UiSkinBuilder.cs`

Mismo patrón que `HudBuilder.cs`. Genera:
- `btn_frame.png` — marco 9-slice con corte en esquina sci-fi, point filter, PPU=50
- `btn_glow.png` — sprite de halo aditivo para el efecto glow de borde
- `scanline_tile.png` — tile 1×2 semitransparente para el overlay CRT

Menú Unity: `SpaceSpaceSpace/Build UI Skin`. Idempotente (re-ejecutable).

### 3. Comportamiento de botón — `Assets/_Project/Scripts/UI/NeonButton.cs`

Subclase de `Button` (preserva toda la funcionalidad del sistema Unity de events/navigation).
- Crea un `Material` instancia con `UINeon.shader` en `Awake` (nunca compartido).
- Implementa `ISelectHandler` / `IDeselectHandler` / `ISubmitHandler` / `IPointerClickHandler`.
- En cada transición de estado: lerp de `_GlowIntensity` + color → corresponde al estado.
- `OnSubmit`/`OnPointerClick`: scale-punch (corrutina sin dependencias externas), flash del material, SFX.

### 4. Navegación — `Assets/_Project/Scripts/UI/MenuNavigation.cs`

Componente que en `OnEnable` fija el primer botón seleccionado via `EventSystem.current.SetSelectedGameObject`.
Asegura que el `EventSystem` usa `InputSystemUIInputModule` (requerido por el nuevo Input System).

### 5. Overlay CRT — `Assets/_Project/Scripts/UI/CrtOverlay.cs` *(opcional)*

`Image` a pantalla completa (último hijo del Canvas de popups) con el tile de scanlines + viñeta.
No toca el juego; es un elemento de UI puro.

### 6. Herramienta de skinado — parte de `UiSkinBuilder.cs`

Además de generar assets, recorre los botones existentes (`Btn_backtomenu`, botones del menú) y:
- Asigna el material neón instancia.
- Añade/reemplaza `Button` → `NeonButton` preservando los eventos OnClick ya configurados.
- Cablea `MenuNavigation` en los Canvas que contengan botones.

---

## Archivos a crear

| Archivo | Rol |
|---|---|
| `Assets/_Project/Art/Shaders/UINeon.shader` | Shader neón/CRT compatible con Canvas |
| `Assets/_Project/Scripts/UI/NeonButton.cs` | Estados + juice + navegación joystick |
| `Assets/_Project/Scripts/UI/MenuNavigation.cs` | Botón por defecto + navegación |
| `Assets/_Project/Scripts/UI/CrtOverlay.cs` *(opc.)* | Scanlines/viñeta pantalla completa |
| `Assets/_Project/Scripts/Editor/UiSkinBuilder.cs` | Genera assets + skinea todos los botones |
| Fuente pixel TMP | En `Assets/_Project/Art/Fonts/` |

---

## Fases de ejecución (orden)

1. Importar fuente pixel como TMP Font Asset.
2. Escribir `UINeon.shader` + generar sprites procedurales vía `UiSkinBuilder`.
3. Implementar `NeonButton` (estados + glow + punch), aplicar a un botón de prueba.
4. `UiSkinBuilder` completo: genera + skinea todos los botones + cablea navegación.
5. Overlay CRT + SFX (pulido final).

---

## Ideas para los fondos *(el usuario los diseña)*

- Rejilla en perspectiva con horizonte y sol con scanlines (synthwave clásico); parallax lento.
- Campo de estrellas en capas + viñeta oscura en bordes.
- Gradiente cian→magenta muy tenue detrás del menú.
- Para combate: fondo casi negro + grid tenue → el bullet-hell se lee bien.

---

## Verificación (cuando se implemente)

1. Play en escenas `Menu` y `Game`.
2. Joystick/teclado: moverse entre botones → el botón **Selected** se enciende con glow pulsante.
3. Pulsar: flash + hundido + sonido. Sin ratón necesario.
4. Scanlines visibles en la UI sin emborronar sprites del juego.
5. Re-ejecutar `UiSkinBuilder` no duplica componentes ni materiales (idempotente).
