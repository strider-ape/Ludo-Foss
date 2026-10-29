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
- **Play vs computer**: on the menu, set each colour to Player, Computer or Off
- **Pass & play**: 2 to 4 people on one phone
- **Classic layout**: Blue bottom-left, then clockwise Red, Green and Yellow, every game. Blue moves first
- **Classic rules**: a 6 to leave base, an extra turn on a 6, a capture or reaching home, safe star squares, an exact roll to finish

Every helper below can be switched on or off in Settings:

- **Move preview**: before you choose, each pawn's landing square lights up, with a dotted route. A red burst means that move captures someone; a shield means a safe square; a crown means home
- **Play for all places**: after the winner, keep going for 2nd and 3rd and finish on a podium. **Skip to results** finishes a computers-only ending instantly
- **Resume**: the game saves after every turn, so **Continue** picks up where you left off, even after the app was closed
- **Turn timer** (off by default): 10, 15 or 30 seconds, shown as a ring around your avatar. When it runs out, the game either plays the move for you or skips the turn
- **2D board** (off by default): a flat, top-down board in the classic printed style, with white squares and thin grey lines, outlined stars, an arrow into each home column and map-pin pawns. The 3D board stays the default
- **Pawn style**: Outline (default: bigger pawns with a dark cartoon edge, easy to see on any square, including their own colour), Outline + coin, or Classic
- **Flat 2D dice** (default): a big, face-on die that is always easy to read; switch to the 3D die in Settings
- **Colour shapes** for colour-blind players: circle (red), triangle (green), square (yellow) and star (blue) on pawns, bases, home triangles and avatars
- **Bigger board** on small phones:
  - **Edge-to-edge board**: a thin frame and no side margins, so the board fills the screen width
  - **Zoom to choices**: when you pick a pawn, the camera glides in on your choices (up to about twice the size), then back out
  - **Generous taps**: a tap anywhere near a glowing pawn picks the nearest one
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

Add `-demo-capture <folder>` to have it play a computer-only match, save four screenshots and quit. Add `-demo-flat` as well to play it on the 2D board.

## Install on your phone

1. On your phone, open the **[latest release](https://github.com/strider-ape/Ludo-Foss/releases/latest)** and download `LudoFOSS-vX.Y.Z.apk`.
2. Open the downloaded file. Android asks to allow **Install unknown apps** for your browser or file manager: allow it once.
3. Tap **Install**, then open **Ludo FOSS**.

**Updating:** install the new APK over the old one; your saved game and settings stay. Only v0.3.0 needs uninstalling first, because it was signed with a temporary key.

**Automatic updates (optional):** [Obtainium](https://github.com/ImranR98/Obtainium) can watch this repository and update the game for you. Add `https://github.com/strider-ape/Ludo-Foss` as an app.

**Check a download (optional):** every release lists the APK's SHA-256 and the signing certificate. Official builds are always signed by `CN=Ludo FOSS, O=strider-ape` (certificate SHA-256 `2e:d6:35:9e:5a:42:b1:d7:95:f7:2e:d3:db:37:64:17:8d:4d:61:d4:16:1d:83:a4:ee:5c:14:fd:87:a4:b0:d6`).

Building it yourself instead? See [Build the APK](#build-the-apk); the file lands in `Builds/LudoFOSS.apk`.

## Adding mods

- Speed and timing: `GameSettings.Scale` and the durations in `Match.cs`
- Animations: `Render/Pawn3D.cs` (hops, captures) and `Render/Dice3D.cs` (the roll)
- 2D board look (colours, lines, stars, map pins): `Render/FlatBoard.cs`
- Rules: `Core/Rules.cs` (legal moves, captures, extra turns)
- Bot behaviour: `Core/Bot.cs` (move scoring)
- Settings screen: `GameRoot.OpenSettings`

## Credits

- Sound effects: [Kenney](https://kenney.nl) (Casino Audio, Interface Sounds, Impact Sounds, Music Jingles), CC0 public domain
- Font: [Lilita One](https://fonts.google.com/specimen/Lilita+One) by Juan Montoreano, SIL Open Font License 1.1

## License

GPL-3.0. See [LICENSE](LICENSE). You may use, study, change and share this game; if you share a modified version, it must stay open source under the same license.
