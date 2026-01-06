# Changelog

All notable changes to this project are documented in this file.

This project follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format and uses [Semantic Versioning](https://semver.org/).

---

## [2.0.1] – 2025-06-05

### Added
- Locked editor window functionalities during Play Mode to prevent unintended changes.
- Added Scroll View to the Audio Creator window, ensuring proper layout when resizing to smaller dimensions.

### Fixed
- Resolved persistent issue causing playlists to stop playing unexpectedly.

---

## [2.0.0] – 2025-06-04

### Added
- Ability to construct audio objects without providing an AudioClip in the constructor.
- New `SetClipByIndex(int)` method to assign a clip by its index.
- Support for initializing audio objects at the point of variable declaration.
- Converted Sounds Good into a Unity Package Manager (UPM) package.
- Option to select a distance-based volume curve (includes two built-in curves and support for a custom curve).
- Added `SetDopplerLevel` method to adjust Doppler effect at runtime.
- Added `SetDynamicMusic` method (before only configurable through the constructor).
- Error handling when passing an empty array to a Playlist or Dynamic Music.
- `Playlist.Shuffle()` method to randomize playback order.
- New property on `Playlist` to query the current ordered list of clips.
- `SetPlayProbability` method to define playback probability for a Sound.
- New methods in `SoundsGoodManager`:
  - Generic `Pause(id)` and `Stop(id)` methods to replace deprecated pause/stop methods.
  - `Resume(id)` and `ResumeAll()` to resume specific or all playing audio.
- Context menu integration in the Unity Editor to create Sounds Good prefabs directly in the scene (GameObject > Sounds Good).
- “Open Demo Scene” button added under Tools > Melenitas Dev > Sounds Good for quick access to the demo scene.

### Changed
- Removed dependency on the **Resources** folder; the user’s sounds, music database, and outputs are now automatically generated under `Assets/SoundsGood/Data`.
- Renamed class `AudioManager` to `SoundsGoodManager` for clarity.
- Renamed class `SourcePoolElement` to `SoundsGoodAudioSource`.
- Moved Prefabs folder out of the Demo folder and Demo assembly to isolate core assets.
- Updated Playlist and Dynamic Music construction to accept parameters (`params`) for easier initialization.
- Audio outputs now automatically set their last saved volume before use.
- Improved the UI layout and styling of all Sounds Good Editor windows.
- Changed `SoundsGoodAudioSource` class to `internal` to hide implementation details.
- Removed “hear distance” option from the `SetVolume` method (all overloads using hear distance are now deprecated).

### Deprecated
- Marked `AudioManager.GetLastSavedVolume` as obsolete; last saved volume is now updated automatically.
- Deprecated all `SetVolume` overloads that accepted a “hear distance” parameter.
- Deprecated the following methods in `SoundsGoodManager`:
  - `PauseSound(id)`
  - `PauseMusic(id)`
  - `StopSound(id)`
  - `StopMusic(id)`
  - `PauseAllMusic()`
  - `PauseAllSound()`

### Fixed
- Fixed bug where AudioCollection search was case-sensitive and did not recognize uppercase characters.
- Corrected issue where a tag did not update properly on `Update` (it only changed in the enum).
- Resolved user database loss when upgrading to version 2.0.0 (Version Upgrader window).
- Fixed playlist stopping unexpectedly after playing for a while.
- Fixed demo script errors that occurred when demo audio clips were removed from Audio Collection.
- Fixed bug in playlists where changing songs could overwrite another audio source’s output if it was still in use.
- Minor adjustments to ensure outputs load the correct volume upon initialization.

### Documentation
- Significantly improved internal documentation and code comments across all Sounds Good classes and methods.

---

## [1.0.1] – 2023-12-04

### Added
- Introduced a property to query playback volume.

### Fixed
- Fixed compilation errors on Unity 2021.1.x and earlier versions.

---

## [1.0.0] – 2023-11-30

### Added
- First public release of Sounds Good.

---
