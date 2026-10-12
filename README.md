# Ludo FOSS

A free, open-source Ludo game for Android. 3D board, smooth animations, your own speed, and nothing else: no ads, no accounts, no tracking, no internet.

## Download

**[Get the latest APK from Releases](https://github.com/strider-ape/Ludo-Foss/releases/latest)** (Android 7.1+). Open it on your phone and allow "Install unknown apps" when asked.

<p>
  <img src="docs/menu.png" width="200" alt="Main menu">
  <img src="docs/match.png" width="200" alt="Choosing a move, with the landing preview and turn timer">
  <img src="docs/winner.png" width="200" alt="Results podium">
  <img src="docs/settings.png" width="200" alt="Settings">
</p>

## Features

- **3D board and pieces**: glossy pawns that hop square to square, a die that really tumbles, confetti when a pawn gets home, a knock-back with a little screen shake on captures
- **Speed control** from 1x (classic pace) up to **Instant**, adjustable mid-game
- **Play vs computer**: any colour can be a bot
- **Pass & play**: 2 to 4 people on one phone; your colour is turned to the bottom-left
- **Classic rules**: a 6 to leave base, an extra turn on a 6, a capture or reaching home, safe star squares, an exact roll to finish

Every helper below can be switched on or off in Settings:

- **Move preview**: before you choose, each pawn's landing square lights up, with a dotted route. A red burst means that move captures someone; a shield means a safe square; a crown means home
- **Play for all places**: after the winner, keep going for 2nd and 3rd and finish on a podium. **Skip to results** finishes a computers-only ending instantly
- **Resume**: the game saves after every turn, so **Continue** picks up where you left off, even after the app was closed
- **Turn timer** (off by default): 10, 15 or 30 seconds, shown as a ring around your avatar. When it runs out, the game either plays the move for you or skips the turn
- **Pawn style**: Outline (default: bigger pawns with a dark cartoon edge, easy to see on any square, including their own colour), Outline + coin, or Classic
- **Colour shapes** for colour-blind players: circle (red), triangle (green), square (yellow) and star (blue) on pawns, bases, home triangles and avatars
- **Auto-move**: when only one move is possible, it happens by itself
- **Three 6s in a row lose the turn**
- **Sound**, **vibration** and **battery saver**

## Privacy and battery

- The APK requests **no permissions except vibration**. Without the internet permission, Android won't let it go online at all.
- No ad, analytics or tracking libraries.
- **Battery friendly**: the game only draws at full speed while something moves. While it waits for your tap it drops to 10-20 frames per second. The 3D is deliberately simple (two small custom shaders, one fixed light, painted shadows), and **Battery saver** in Settings caps animations at 30 fps.

## Project layout

```
Assets/
  Scripts/Core/         Pure C# game rules (no Unity dependency): board, moves, captures, bot
  Scripts/Game/         Match flow, menus, settings, sound, vibration
  Scripts/Game/Render/  3D board, pawns, die, camera, particles, frame-rate governor
  Scripts/Dev/          Screenshot helper for desktop preview builds (inactive in normal play)
  Editor/               Android build, Windows preview build, rules self-test
  Resources/            Shaders, sounds and font
  Scenes/Main.unity     Empty scene; everything is built from code at startup
```

Meshes and textures (board, pawns, die, icons) are generated in code, so there are no model or image files to manage.

## Build the APK

Requires Unity **6000.3.25f1** with Android Build Support.

From the Unity editor: **Ludo > Build Android APK**. Or from a terminal:

```
Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod LudoGame.EditorTools.BuildAndroid.Build -logFile build.log
```

The APK is written to `Builds/LudoFOSS.apk`.

Every Android build runs a **privacy check** on the finished APK and fails if it finds any permission other than vibration, or code from known ad, analytics, attribution or crash-reporting SDKs (Firebase, AdMob, Facebook, AppLovin, Unity Ads and many more). You can point it at any APK to compare:

```
Unity.exe -batchmode -quit -projectPath . -executeMethod LudoGame.EditorTools.PrivacyGuard.CheckApk -apk path\to\other.apk -logFile guard.log
```

### Release signing (maintainers)

Release builds are signed with the project's permanent key, which is **not** in this repository. The build looks for `%USERPROFILE%\.ludofoss\signing.properties` (or the file named by the `LUDO_SIGNING` environment variable):

```
storeFile=ludofoss-release.keystore
storePassword=...
keyAlias=ludofoss
keyPassword=...
```

Without it the build still works, but the APK is debug-signed and can't update an installed release build.

The app icon is drawn in code: **Ludo > Generate App Icon** writes the PNGs in `Assets/Icons`.

## Test the rules

**Ludo > Run Self Test**, or:

```
Unity.exe -batchmode -quit -projectPath . -executeMethod LudoGame.EditorTools.SelfTest.Run -logFile test.log
```

This checks the board geometry and rules, then plays 400 computer-only games.

## Preview on Windows (no phone needed)

**Ludo > Build Windows Preview** (or `-buildTarget Win64 -executeMethod LudoGame.EditorTools.WindowsPreview.Build`), then:

```
Builds\Windows\LudoFOSS.exe -screen-width 540 -screen-height 1170
```

Add `-demo-capture <folder>` to have it play a computer-only match, save four screenshots and quit.

## Install on your phone

Copy `Builds/LudoFOSS.apk` to the phone, open it, and allow "Install unknown apps" for your file manager when Android asks.

## Adding mods

- Speed and timing: `GameSettings.Scale` and the durations in `Match.cs`
- Animations: `Render/Pawn3D.cs` (hops, captures) and `Render/Dice3D.cs` (the roll)
- Rules: `Core/Rules.cs` (legal moves, captures, extra turns)
- Bot behaviour: `Core/Bot.cs` (move scoring)
- Settings screen: `GameRoot.OpenSettings`

## Credits

- Sound effects: [Kenney](https://kenney.nl) (Casino Audio, Interface Sounds, Impact Sounds, Music Jingles), CC0 public domain
- Font: [Lilita One](https://fonts.google.com/specimen/Lilita+One) by Juan Montoreano, SIL Open Font License 1.1

## License

GPL-3.0. See [LICENSE](LICENSE). You may use, study, change and share this game; if you share a modified version, it must stay open source under the same license.
