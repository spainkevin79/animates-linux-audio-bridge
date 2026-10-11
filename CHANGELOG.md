# Changelog

## 0.3.0 beta — public-ready packaging (2026-10-11)

- Combined the user-tested BepInEx v0.2.0 plugin and v0.2.1 relay/launcher behavior in one source tree.
- Added configurable installation paths, local compilation, an additional KDE menu entry, backup behavior and uninstall.
- Added documentation, Python tests and CI syntax checks.
- No intentional changes to the previously tested C# music/dance behavior or Python relay.

## 0.2.1 — local one-click launcher

- Automatically start and clean up the PipeWire monitor relay using a wrapper around the existing Proton launcher.

## 0.2.0 — local speech-aware bridge

- Reuse Animates' music analyzer and dance controller via its `IAudioCaptureSource` bridge.
- Temporarily suppress dancing while the character speaks, with configurable cooldown.
- Confirmed Spotify and Elisa playback response and preserved microphone operation on one system.