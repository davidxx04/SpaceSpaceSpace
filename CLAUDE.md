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
  Scripts/Core/     game systems (GameManager); Core/Pooling/ = reusable object pool (see "Object pooling")
  Scripts/Player/   player controller, input reader, state machine
  Scripts/Player/States/  one file per FSM state (LocomotionState, RollState, AttackState, ParryState)
  Scripts/Enemy/Boss/  boss FSM, attack director + ScriptableObject attacks (see Scripts/Enemy/Boss/README.md)
  Scripts/Combat/   damage contract + shared combat components (IDamageable, IParryable, Health, Hitbox, Projectile, DamageFlash)
  Scripts/Data/     ScriptableObject data, split by owner: Data/Player/ (RollData, AttackData, ParryData, AfterimageData), Data/Boss/ (BossPhaseData, BossComboSO, BossMovementData)
  Scripts/Visual/   reusable visual helpers (SpriteFlipbook, SpriteBlink, AfterimageEmitter/Afterimage, SpriteSolidFlash, Shockwave, LightningBurst, CameraShake)
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
- **States** (`States/`): `LocomotionState` (slow walk), `RollState` (curve-driven dash with i-frames + cooldown), `AttackState` (ranged fan-fire — see Combat) and `ParryState` (reactive block window — see Combat). All four are cached on the controller and entered from `PlayerInputReader` events via `OnRollInput/OnAttackInput/OnParryInput`, gated by the current state's `CanInterrupt` + the per-action cooldown.
- **`RollData`** (`Scripts/Data/`, a `ScriptableObject`) — all roll tuning lives in an **asset** edited from the Inspector (distance, duration, `AnimationCurve` for fast→slow feel, i-frame window as 0..1 fractions, cooldown). Asset edits persist through Play mode, so the roll is balanced "hot". New abilities should follow this same SO-per-ability pattern (`AttackData`, `ParryData`).
- **Feedback & VFX glue** — the ship's juice is built from small, generic, reusable **`Scripts/Visual/`** helpers (`SpriteFlipbook`, `SpriteBlink`, `AfterimageEmitter`/`Afterimage`, `SpriteSolidFlash`, `Shockwave`, `LightningBurst`, `CameraShake`) driven by thin **glue** components in `Scripts/Player/` (`ShipThruster`, `PlayerHurtFx`, `ParryAura`, `ParrySuccessFx`). **Pattern to follow:** the glue reads only `PlayerController`'s public API / `PlayerDamageReceiver` events and toggles the generic helpers — combat/gameplay stay unaware of any VFX. The dash trail opts in via `RollData.afterimage`. Full per-component detail (params, shaders, quirks like `flipY` not being inherited) lives in **`Scripts/Visual/README.md`**.

**Mercy i-frames after a hit:** `PlayerController.IsInvulnerable` is now the OR of two sources — the roll's i-frame window (written via the setter by `RollState`, unchanged) and a timed window opened by `PlayerController.GrantInvulnerability(seconds)` (keeps the latest expiry). `PlayerDamageReceiver` calls `GrantInvulnerability(hitInvulnerabilitySeconds)` in its real-damage branch (before firing `Hit`), so a landed hit grants brief invulnerability that blocks chained hits.

**Conventions for player code:** movement/physics writes go in `FixedTick` (use `rb.velocity` — this is Unity 2022.3, *not* `linearVelocity`); the curve-driven roll uses `rb.MovePosition`. `IsInvulnerable` is the hook the (future) damage system will read for dodge i-frames.

## Combat & damage

Damage flows through a small, reusable contract so the player, the boss, and the boss's destructible parts all take damage uniformly (all under `Assets/_Project/Scripts/Combat/`):

- **`IDamageable`** — single method `TakeDamage(DamageInfo)`; anything hittable implements it.
- **`DamageInfo`** — a `struct` (amount, source, direction) passed by value, so new fields (knockback, damage type, parryable…) can be added without changing every signature.
- **`Health`** — implements `IDamageable`; numeric `Current`/`Max` decoupled from UI via `Damaged`/`Died` events. `Current` uses `[field: SerializeField]` so it's visible in the Inspector for live debugging.
- **`Hitbox`** — reusable **trigger** collider; while enabled, applies its configured `DamageInfo` to any `IDamageable` it overlaps on `targetLayers` (deduped per activation), and raises a `Hit` event on each landed hit. Disabled by default; whoever fires it calls `Activate(DamageInfo)`. It powers the player's **projectiles** (the trigger approach was chosen over an `OverlapBox` query precisely so bullets reuse it).
- **`Projectile`** — moves a bullet along a direction at `speed` until it travels its `range` or hits (then despawns unless `pierce`); carries a `Hitbox`, is fired via `Launch(...)`, needs a Kinematic `Rigidbody2D`. **Pooled** (see "Object pooling"): `Despawn` returns it to the pool instead of `Destroy`, and it subscribes `hitbox.Hit` **once in `Awake`** (not per-`Launch`) so reuse can't double-subscribe; `Launch` resets the rest. The boss reuses the same component (`BossBullet` prefab) for its bullet patterns.
- **`DamageFlash`** — optional feedback; flashes the sprite on `Health.Damaged`.

The player attack (`AttackState` + `AttackData`, same FSM+SO pattern as the roll) is **ranged / bullet-hell**: at `fireTime` it spawns `projectileCount` bullets in a `spreadAngle` fan toward `AimDirection` (`projectileSpeed`, `range`, `pierce`), scales movement while firing by `moveMultiplier`, and applies **recoil** via `PlayerController.ApplyRecoil` — a visual sprite bounce or a physical body push, chosen by `recoilMovesPlayer`. `cooldown` gates fire rate; `cancelWindow` cancels the recovery. Player bullets target the `Enemy` layer. **Keep the Player root at scale 1**: visuals live on the `Visual` child, which `UpdateAim` rotates/flips to aim and which also carries the recoil offset.

**Parry** is reactive, not offensive: `ParryState` + `ParryData` open a timing window during which `PlayerController.IsParrying` is true (analogous to `IsInvulnerable`). Enemy attacks reach the player through **`PlayerDamageReceiver`** (the player's `IDamageable`), which intercepts in order: parry (`IsParrying && DamageInfo.parryable`) → dodge i-frames (`IsInvulnerable`) → (future) `Health`. A successful parry fires `PlayerDamageReceiver.ParrySuccess` and notifies the attacker via `IParryable.OnParried()` (stun/posture hook). Whether a hit can be parried travels on the hit itself (`DamageInfo.parryable`), not via a physics layer. `TestEnemyAttacker` (`Scripts/_Testing/`) is a throwaway parryable attacker for testing parry without the boss — delete it (file + scene object) later.

## Boss architecture

The boss mirrors the player's **FSM + ScriptableObject** philosophy but splits three concerns so attacks/combos scale without code churn (all under `Scripts/Enemy/Boss/`; **full detail in `Scripts/Enemy/Boss/README.md`**):

- **`BossAttackSO`** — each attack is a polymorphic **ScriptableObject asset** with a coroutine `Execute(BossContext)` (telegraph → active → recovery). It marks `parryable` on its `DamageInfo` (bullet-hell = false, sekiro = true) and spawns through **`BossContext`** — the bridge that hands SO attacks the scene refs + a `Spawn` helper, reusing `Projectile`/`Hitbox`/the pool. Add an attack = new subclass + asset; nothing else changes. SO assets are shared, so per-run state stays in coroutine locals (never in fields).
- **`BossComboSO`** — a fixed, ordered sequence of attacks + delays (what the player memorizes).
- **`BossAttackDirector`** — picks singles vs combos and paces them; parametrized by the current **`BossPhaseData`** (repertoire + aggressiveness), which `BossController` swaps by boss-health thresholds.
- **`BossController`** — the hub: high-level FSM `Intro → Combat → Stagger → Defeated` (`BossStateMachine`/`IBossState`), and implements `IParryable` so parries build posture → `Stagger` **without** aborting the in-flight combo (keeps it memorizable). `BossMovement` is a separate `FixedUpdate` component; destructible scoring parts are `BossPart` (own `Health`, regen via `ResetHealth`).

## Object pooling

To avoid GC hitches from spawning/destroying many bullets per second, frequently-created objects go through a reusable pool instead of `Instantiate`/`Destroy` (all under `Scripts/Core/Pooling/`):

- **`PoolManager`** — a `DontDestroyOnLoad` singleton with a **static API**: `PoolManager.Spawn(prefab, pos, rot)` / `Spawn<T>(...)` and `PoolManager.Despawn(instance)`. Persistent so pools **prewarm at startup (even in the Menu)** to avoid first-fight hitches, and on every `SceneManager.sceneLoaded` it returns all active instances to their pools (clean slate per run — no bullets carried across scenes). A serialized **preload list** (`{prefab, prewarm, maxSize}`) is warmed in `Awake`. Place one `PoolManager` GameObject in the **Menu** scene (alongside `GameManager`) and list the player bullet prefab + `BossBullet` there. If none exists it lazily auto-creates (no prewarm) so the static API never fails.
- **`GameObjectPool`** — the engine, deliberately **not married to prefabs**: built from a `Func<GameObject>` factory + an arbitrary key (the prefab path is just sugar — `Spawn(prefab)` uses the prefab as key and `Instantiate(prefab)` as factory; for non-prefab objects use `Spawn(key, factory, …)`). Tracks idle/active and is **prewarm N → grow on demand → hard `maxSize`**; at the cap it recycles the oldest active instance (+warning), so it never returns null or leaks entities.
- **`PooledObject`** — auto-added to each instance; remembers its pool so it can self-return (`Release()`), falling back to `Destroy` if it wasn't pooled.
- **`IPoolable`** — optional `OnSpawned`/`OnDespawned` hooks the pool calls on reuse, for objects whose reset isn't already handled elsewhere (`Projectile` doesn't need it because `Launch` resets everything).

**Convention:** anything spawned in bulk (bullets now; future area/other-projectile attacks) should be obtained via `PoolManager.Spawn` and returned via `PoolManager.Despawn` / `PooledObject.Release`, **not** `Instantiate`/`Destroy`. A prefab-keyed pool must be fed the **same prefab reference** the caller uses (e.g. `AttackData.projectilePrefab`, `BossController.bossProjectile`) so callers share one pool. One-shot VFX (shockwave, lightning, muzzle flash) still self-`Destroy` for now — candidate pool migrations later. The bullet firing sites are `AttackState.Fire` (player) and `BossContext.Spawn` (boss).