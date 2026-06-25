# Visual helpers & VFX

Generic, **reusable** visual components (this folder) plus the thin player-side **glue** that drives
them. Design rule: the helpers are gameplay-agnostic and parametrizable; the glue reads only public
API / events and toggles the helpers, so **combat/gameplay never reference VFX**. A future enemy/boss
reuses the same helpers with its own glue and data — no new helper code.

## Reusable helpers (`Scripts/Visual/`)

- **`SpriteFlipbook`** — reusable code flipbook: cycles a `Sprite[]` at a settable `FramesPerSecond`.
  Supports `loop` (default), `pingPong` (forward then back — "que vuelva") with optional
  `pingPongHoldSeconds` lingering on the last frame before returning, one-shot playback (`loop` off),
  a `Finished` event, and `disableObjectOnFinish` (a non-looping one-shot hides its own GameObject when
  done) — so the same component drives looping VFX and fire-and-forget overlays alike.

- **`SpriteBlink`** — generic blinker: while `Active`, toggles its `SpriteRenderer.enabled` at
  `blinksPerSecond`, restoring visibility when stopped/disabled.

- **`AfterimageEmitter` / `Afterimage`** (+ **`AfterimageData`** SO) — dash "echo" trail: while emitting
  it leaves fading frozen copies (`Afterimage`) of a source `SpriteRenderer`, spawned **un-parented in
  world space** so they stay behind as the ship dashes. Each ghost renders as a **solid silhouette** in
  a chosen colour via the unlit `AfterimageSolid` shader (`Art/Shaders/`), whose material the emitter
  **creates in code** (`Shader.Find`, cached static, shared) so nothing needs wiring; per-ghost colour
  rides on `SpriteRenderer.color`. Tuned by `AfterimageData` (`spawnInterval`, `lifetime`, `Color color`
  default white, `AnimationCurve alphaOverLife` fade, `sortingOffset`, optional `material` override).
  Opt in from an ability via its data (e.g. `RollData.afterimage`); the state calls
  `StartEmitting/StopEmitting`. **For built players, add `AfterimageSolid` to *Always Included Shaders***
  so `Shader.Find` survives stripping.

- **`SpriteSolidFlash`** — brief white-ish solid flash of a sprite; reuses the `AfterimageSolid`
  material. Used as parry "ship flash".

- **`Shockwave`** — expanding, fading ring (prefab); self-`Destroy`s after its duration.

- **`LightningBurst`** — generic procedural lightning. **Generates the bolt geometry in C#** (jagged
  midpoint-displaced polylines radiating from the origin, with parametrizable random ranges — bolt
  count, length, width, jaggedness/deviation, branches — plus a `verticality` mode for a big up+down
  bolt) and renders each bolt/branch with a pooled `LineRenderer` using a shared additive glow shader
  (`Art/Shaders/Lightning.shader`, hot white core via the line's cross-width UV). Life-fade (and an
  optional electric `flicker` that regenerates the shape) animate per frame; a `loop` toggle helps
  tuning. It's **generated geometry, not a pure fragment shader**, because the randomness/forking lives
  naturally in code; the shader only does the glow.

- **`CameraShake`** — decaying positional camera offset, for impact feel.

## Player VFX glue (`Scripts/Player/`)

These are thin adapters: they read only `PlayerController` public API / `PlayerDamageReceiver` events
and drive the helpers above.

- **`ShipThruster`** — exhaust/turbo glue on a **child of the `Visual`** (inherits the ship's
  rotation/position for free; only syncs `flipY`, a `SpriteRenderer` prop that is **not** inherited).
  Picks the animation by state — moving → `flightFrames`, rolling → `turboFrames`, idle → off — driving
  a `SpriteFlipbook`. Reads only `IsRolling`, `ShipFacingLeft`, `Input.MoveInput`.

- **`PlayerHurtFx`** — hit-feedback glue on the Player root. Subscribes to `PlayerDamageReceiver.Hit`
  (fired only on real damage, never on parry/i-frames) and re-activates a `hurtOverlay` child whose
  `SpriteFlipbook` is a one-shot `pingPong` (optionally holding the last damaged frame) with
  `disableObjectOnFinish`, so a damaged-sprite flash plays and self-hides; chained hits restart it
  (toggle off→on). The overlay is a child of `Visual`, so it inherits rotation but not `flipY` → it syncs
  `overlaySr.flipY = player.ShipFacingLeft` while active. Also drives an optional `SpriteBlink` during
  the post-hit i-frames: on `Hit` it latches a blink lasting exactly while `player.IsInvulnerable` stays
  true (a plain roll, which never fires `Hit`, doesn't blink).

- **`ParryAura`** — shows a shrinking ring while the block window is open (reads `IsParrying` +
  `ParryWindowProgress`, which `ParryState` writes via `InverseLerp` of the window). Subscribes to
  `PlayerDamageReceiver.ParrySuccess`/`Hit` and **dismisses itself the instant the parry resolves**
  (landed or got hit), re-arming when the window closes.

- **`ParrySuccessFx`** — subscribes to `PlayerDamageReceiver.ParrySuccess` and fires an optional combo
  of helpers: `SpriteSolidFlash`, a `Shockwave` prefab, a `LightningBurst` prefab, `CameraShake`, and a
  sparks prefab. All refs are optional/null-checked so each piece can be toggled to test combos.
