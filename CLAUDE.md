# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

SpaceSpaceSpace is a Unity **2022.3.62f2** (LTS) 2D game in early development. It uses the **Universal Render Pipeline (URP)** and the new **Input System** (`com.unity.inputsystem`). The pinned editor version is in `ProjectSettings/ProjectVersion.txt` — open the project with exactly this version to avoid asset upgrades.

## Building, running, and tests

There is no command-line build/test workflow set up. Work happens through the Unity Editor:

- **Run:** open the project in Unity and press Play, starting from `Assets/_Project/Scenes/Menu.unity` (the first scene in Build Settings).
- **Tests:** the Unity Test Framework package is installed but no tests exist yet. When added, run them via **Window → General → Test Runner** (EditMode / PlayMode tabs). There are no assembly definitions, so all scripts compile into the default `Assembly-CSharp`. The many `*.csproj` / `*.sln` files at the repo root are Unity-generated IDE scaffolding — do not edit them by hand; Unity regenerates them.

## Source layout convention

All first-party content lives under `Assets/_Project/` (the leading underscore sorts it above Unity's package folders). Third-party assets go in `Assets/ThirdParty/`. **Keep this separation** — do not scatter game scripts elsewhere under `Assets/`.

```
Assets/_Project/
  Scripts/Core/     game systems (e.g. GameManager)
  Scripts/Player/   player controller, input reader, state machine
  Scripts/Player/States/  one file per FSM state (LocomotionState, RollState, AttackState, ParryState, ...)
  Scripts/Combat/   damage contract + shared combat components (IDamageable, IParryable, Health, Hitbox, Projectile, DamageFlash)
  Scripts/Data/     ScriptableObject data assets (RollData, AttackData, ParryData, AfterimageData, ...)
  Scripts/Visual/   reusable visual helpers (SpriteFlipbook, AfterimageEmitter/Afterimage, SpriteSolidFlash, Shockwave, LightningBurst, CameraShake)
  Scripts/_Testing/ throwaway test props (TestEnemyAttacker) — safe to delete
  Input/            input action assets + generated wrappers
  Scenes/           Menu, Game, Leaderboard
  Art/  Audio/  Prefabs/
```

## Architecture

**Scene flow** is driven by `GameManager` (`Assets/_Project/Scripts/Core/GameManager.cs`): a `DontDestroyOnLoad` singleton (`GameManager.Instance`) that persists for the whole session and owns all scene transitions. Scene names are constants on this class (`MenuScene`, `GameScene`). It tracks a high-level `GameState` (`Menu` / `Playing`) by listening to `SceneManager.sceneLoaded`, and resets `Time.timeScale` to 1 on every load so a paused game can't carry a frozen timescale into the next scene. Route scene changes and quit through `GameManager` (`LoadMenu` / `StartGame` / `QuitGame`) rather than calling `SceneManager` directly. Note: Build Settings includes a `Leaderboard` scene that does not yet have a corresponding constant/method on `GameManager`.

**Input** is defined in `Assets/_Project/Input/ArcadeControls.inputactions` and code-generated into `ArcadeControls.cs`. That `.cs` file is **auto-generated — never edit it**; change bindings in the `.inputactions` asset (via the Unity Input Actions editor) and let Unity regenerate. The single `Player` action map currently exposes: `Move` (Vector2, WASD), `Rol` (J), `Attack` (K), `Parry` (L). Do **not** consume `ArcadeControls` directly from gameplay code — go through `PlayerInputReader` (see below).

## Game design (intended)

A high-difficulty arcade boss-rush "duel": one ship vs. one boss in a closed arena, short runs (~10 min). The player has exactly **three verbs — dodge (roll) / parry / attack** — and the fun is learning the boss's attack combos (à la Furi / Punch-Out!!) and answering each with the right verb. Planned systems (mostly not built yet): boss with destructible+regenerating parts that grant score, a persistent high-score table, a "coming soon" card on boss defeat.

**Deployment target is an arcade cabinet** at Museo Arcade Vintage. Cabinets typically route their joystick/buttons through a keyboard encoder (I-PAC/JAMMA) that emits **keystrokes**, which is exactly why the current bindings live on the keyboard map — keep input data-driven so remapping to the cabinet stays trivial.

## Player architecture

The player is built as a small **finite state machine + ScriptableObject tuning data**, deliberately kept modular so attack/parry slot in as new files without touching existing ones. Key pieces (all under `Assets/_Project/Scripts/Player/`):

- **`PlayerController`** — the MonoBehaviour "hub"/context. Owns the input reader and state machine, exposes shared refs/runtime state that states read & write: `Rb`, `Input`, `RollData`, `AimDirection`, `IsInvulnerable`, `NextRollTime`/`CanRoll`. Runs `Update`→`StateMachine.Tick()` and `FixedUpdate`→`StateMachine.FixedTick()`, and keeps `AimDirection` = last non-zero move input (rotating a child `aimIndicator`).
- **`PlayerInputReader`** — the **only** place that touches `ArcadeControls`. Implements `IPlayerActions`, exposes `MoveInput` (poll) plus `RollPerformed`/`AttackPerformed`/`ParryPerformed` events. States subscribe here; nothing else references the Input System.
- **`PlayerStateMachine` + `IPlayerState`** (`Enter/Tick/FixedTick/Exit`) — `ChangeState` exits the old state then enters the new. State instances are cached on the controller (no per-transition allocations).
- **States** (`States/`): `LocomotionState` (slow walk + listens for roll) and `RollState` (curve-driven dash with i-frames + cooldown). `AttackState`/`ParryState` are future files; the input events for them are already wired.
- **`RollData`** (`Scripts/Data/`, a `ScriptableObject`) — all roll tuning lives in an **asset** edited from the Inspector (distance, duration, `AnimationCurve` for fast→slow feel, i-frame window as 0..1 fractions, cooldown). Asset edits persist through Play mode, so the roll is balanced "hot". New abilities should follow this same SO-per-ability pattern (`AttackData`, `ParryData`).
- **`AfterimageEmitter`** (`Scripts/Visual/`) — reusable dash "echo" trail: while emitting it leaves fading frozen copies (`Afterimage`) of a source `SpriteRenderer`, spawned **un-parented in world space** so they stay behind as the ship dashes. Each ghost renders as a **solid silhouette** in a chosen colour via the unlit `AfterimageSolid` shader (`Art/Shaders/`), whose material the emitter **creates in code** (`Shader.Find`, cached static, shared) so nothing needs wiring; per-ghost colour rides on `SpriteRenderer.color`. Tuned by an **`AfterimageData`** SO (`spawnInterval`, `lifetime`, `Color color` default white, `AnimationCurve alphaOverLife` fade, `sortingOffset`, optional `material` override). The roll opts in via `RollData.afterimage`; `RollState` calls `player.Afterimage.StartEmitting/StopEmitting`. A future harder dash reuses the same emitter with its own SO asset — no new code. (For built players, add `AfterimageSolid` to *Always Included Shaders* so `Shader.Find` survives stripping.)
- **`ShipThruster`** (`Scripts/Player/`) — exhaust/turbo VFX glue. Lives on a **child of the `Visual`** so it inherits the ship's rotation/position for free; it only syncs `flipY` (a `SpriteRenderer` property, not inherited). It picks the animation by state — moving → `flightFrames`, rolling → `turboFrames`, idle → off — and drives a generic **`SpriteFlipbook`** (`Scripts/Visual/`, a reusable code flipbook: `Sprite[]` cycled at a settable `FramesPerSecond`). It reads only `PlayerController` public API (`IsRolling`, `ShipFacingLeft`, `Input.MoveInput`), so nothing else knows the thruster exists.
- **Parry VFX** (`Scripts/Player/`) — `ParryAura` shows a shrinking ring while the block window is open (reads `IsParrying` + `ParryWindowProgress`, which `ParryState` writes via `InverseLerp` of the window), so the player reads the active timing at a glance. It also subscribes to `PlayerDamageReceiver.ParrySuccess`/`Hit` and **dismisses itself the instant the parry resolves** (landed or got hit), re-arming when the window closes. `ParrySuccessFx` subscribes to `PlayerDamageReceiver.ParrySuccess` and fires an optional combo of generic reusable bits — `SpriteSolidFlash` (white-ish ship flash, reuses the `AfterimageSolid` material), a `Shockwave` prefab (expanding fading ring), a `LightningBurst` prefab (procedural branching lightning bolts), `CameraShake` (decaying offset), and a sparks prefab. All refs are optional/null-checked so each piece can be toggled to test combos; the combat code stays unaware. `LightningBurst` (`Scripts/Visual/`) is a generic, reusable lightning effect: it **generates the bolt geometry in C#** (jagged midpoint-displaced polylines radiating from the origin, with parametrizable random ranges — bolt count, length, width, jaggedness/deviation, branches — plus a `verticality` mode for a big up+down bolt) and renders each bolt/branch with a pooled `LineRenderer` using a shared additive glow shader (`Art/Shaders/Lightning.shader`, hot white core via the line's cross-width UV). Life-fade (and an optional electric `flicker` that regenerates the shape) animate per frame; a `loop` toggle helps tuning. Branching lightning is **generated geometry, not a pure fragment shader**, because the randomness/forking lives naturally in code; the shader only does the glow.

**Conventions for player code:** movement/physics writes go in `FixedTick` (use `rb.velocity` — this is Unity 2022.3, *not* `linearVelocity`); the curve-driven roll uses `rb.MovePosition`. `IsInvulnerable` is the hook the (future) damage system will read for dodge i-frames.

## Combat & damage

Damage flows through a small, reusable contract so the player, the boss, and the boss's destructible parts all take damage uniformly (all under `Assets/_Project/Scripts/Combat/`):

- **`IDamageable`** — single method `TakeDamage(DamageInfo)`; anything hittable implements it.
- **`DamageInfo`** — a `struct` (amount, source, direction) passed by value, so new fields (knockback, damage type, parryable…) can be added without changing every signature.
- **`Health`** — implements `IDamageable`; numeric `Current`/`Max` decoupled from UI via `Damaged`/`Died` events. `Current` uses `[field: SerializeField]` so it's visible in the Inspector for live debugging.
- **`Hitbox`** — reusable **trigger** collider; while enabled, applies its configured `DamageInfo` to any `IDamageable` it overlaps on `targetLayers` (deduped per activation), and raises a `Hit` event on each landed hit. Disabled by default; whoever fires it calls `Activate(DamageInfo)`. It powers the player's **projectiles** (the trigger approach was chosen over an `OverlapBox` query precisely so bullets reuse it).
- **`Projectile`** — moves a bullet along a direction at `speed` until it travels its `range` or hits (then despawns unless `pierce`); carries a `Hitbox`, is fired via `Launch(...)`, needs a Kinematic `Rigidbody2D`. The boss will reuse the same component for its bullet patterns.
- **`DamageFlash`** — optional feedback; flashes the sprite on `Health.Damaged`.

The player attack (`AttackState` + `AttackData`, same FSM+SO pattern as the roll) is **ranged / bullet-hell**: at `fireTime` it spawns `projectileCount` bullets in a `spreadAngle` fan toward `AimDirection` (`projectileSpeed`, `range`, `pierce`), scales movement while firing by `moveMultiplier`, and applies **recoil** via `PlayerController.ApplyRecoil` — a visual sprite bounce or a physical body push, chosen by `recoilMovesPlayer`. `cooldown` gates fire rate; `cancelWindow` cancels the recovery. Player bullets target the `Enemy` layer. **Keep the Player root at scale 1**: visuals live on the `Visual` child, which `UpdateAim` rotates/flips to aim and which also carries the recoil offset.

**Parry** is reactive, not offensive: `ParryState` + `ParryData` open a timing window during which `PlayerController.IsParrying` is true (analogous to `IsInvulnerable`). Enemy attacks reach the player through **`PlayerDamageReceiver`** (the player's `IDamageable`), which intercepts in order: parry (`IsParrying && DamageInfo.parryable`) → dodge i-frames (`IsInvulnerable`) → (future) `Health`. A successful parry fires `PlayerDamageReceiver.ParrySuccess` and notifies the attacker via `IParryable.OnParried()` (stun/posture hook). Whether a hit can be parried travels on the hit itself (`DamageInfo.parryable`), not via a physics layer. `TestEnemyAttacker` (`Scripts/_Testing/`) is a throwaway parryable attacker for testing parry without the boss — delete it (file + scene object) later.