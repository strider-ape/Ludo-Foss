# Ludo Turbo

A simple, offline Ludo game for Android, built with Unity. No ads, no accounts, no internet, and you control the speed.

<p>
  <img src="docs/menu.png" width="240" alt="Main menu">
  <img src="docs/match.png" width="240" alt="A match in progress">
  <img src="docs/winner.png" width="240" alt="Winner screen">
</p>

The APK requests no Android permissions, so it can't use the internet, microphone or billing.

## Features

- **Speed control** from 1x (classic pace) up to **Instant**, adjustable mid-game
- **Play vs computer**: any colour can be a bot
- **Pass & play**: 2 to 4 people on one phone
- **Auto-move**: when only one move is possible, it happens by itself
- Classic rules: a 6 to leave base, an extra turn on a 6, a capture or reaching home, safe star squares, an exact roll to finish, and an optional rule where three 6s in a row lose the turn

## Project layout

```
Assets/
  Scripts/Core/   Pure C# game rules (no Unity dependency): board, moves, captures, bot
  Scripts/Game/   Unity layer: board drawing, tokens, dice, menus, turn loop
  Scripts/Dev/    Screenshot helper for desktop preview builds (inactive in normal play)
  Editor/         Android build, Windows preview build, rules self-test
  Scenes/Main.unity  Empty scene; everything is built from code at startup
```

All graphics are generated in code (`SpriteFactory.cs`), so there are no image assets to manage.

## Build the APK

Requires Unity **6000.3.25f1** with Android Build Support.

From the Unity editor: **Ludo > Build Android APK**. Or from a terminal:

```
Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod LudoTurbo.EditorTools.BuildAndroid.Build -logFile build.log
```

The APK is written to `Builds/LudoTurbo.apk`.

## Test the rules

**Ludo > Run Self Test**, or:

```
Unity.exe -batchmode -quit -projectPath . -executeMethod LudoTurbo.EditorTools.SelfTest.Run -logFile test.log
```

This checks the board geometry and rules, then plays 400 computer-only games.

## Preview on Windows (no phone needed)

**Ludo > Build Windows Preview** (or `-buildTarget Win64 -executeMethod LudoTurbo.EditorTools.WindowsPreview.Build`), then:

```
Builds\Windows\LudoTurbo.exe -screen-width 540 -screen-height 960
```

Add `-demo-capture <folder>` to have it play a computer-only match, save four screenshots and quit.

## Install on your phone

Copy `Builds/LudoTurbo.apk` to the phone, open it, and allow "Install unknown apps" for your file manager when Android asks.

## Adding mods

- Speed and timing: `GameSettings.Scale` and the durations in `Match.cs`
- Rules: `Rules.cs` (legal moves, captures, extra turns)
- Bot behaviour: `Bot.cs` (move scoring)
- Settings screen: `GameRoot.OpenSettings`
