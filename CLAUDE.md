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
  Scripts/Core/   game systems (e.g. GameManager)
  Input/          input action assets + generated wrappers
  Scenes/         Menu, Game, Leaderboard
  Art/  Audio/  Prefabs/
```

## Architecture

**Scene flow** is driven by `GameManager` (`Assets/_Project/Scripts/Core/GameManager.cs`): a `DontDestroyOnLoad` singleton (`GameManager.Instance`) that persists for the whole session and owns all scene transitions. Scene names are constants on this class (`MenuScene`, `GameScene`). It tracks a high-level `GameState` (`Menu` / `Playing`) by listening to `SceneManager.sceneLoaded`, and resets `Time.timeScale` to 1 on every load so a paused game can't carry a frozen timescale into the next scene. Route scene changes and quit through `GameManager` (`LoadMenu` / `StartGame` / `QuitGame`) rather than calling `SceneManager` directly. Note: Build Settings includes a `Leaderboard` scene that does not yet have a corresponding constant/method on `GameManager`.

**Input** is defined in `Assets/_Project/Input/ArcadeControls.inputactions` and code-generated into `ArcadeControls.cs`. That `.cs` file is **auto-generated — never edit it**; change bindings in the `.inputactions` asset (via the Unity Input Actions editor) and let Unity regenerate. The single `Player` action map currently exposes: `Move` (Vector2, WASD), `Rol` (J), `Attack` (K), `Parry` (L). Consume input by implementing `ArcadeControls.IPlayerActions` and registering via `AddCallbacks`.