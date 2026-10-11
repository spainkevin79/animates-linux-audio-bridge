# Contributing

Issues, reports and pull requests are welcome. Please test changes against your legally installed copy of Animates and preserve the no-proprietary-binaries rule.

A useful compatibility report contains:
- Linux distribution and desktop session (X11/Wayland)
- PipeWire/PulseAudio and Proton/Wine versions
- Animates game/Unity build and BepInEx version
- Audio monitor and music player used
- Reproduction steps, expected result, observed result
- Relevant `[LinuxAudio]` BepInEx log lines and relay log lines **after removing personal paths and sensitive data**

Avoid attaching application binaries, `Assembly-CSharp.dll`, full disassemblies, proprietary assets, tokens or private conversations. CI checks Python and shell. C# integration needs local managed assemblies and real runtime testing.