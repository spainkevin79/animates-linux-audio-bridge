# Animates Linux Audio Bridge

**Music-responsive dancing for Animation Inc.'s Windows Animates desktop companion running under Proton on Linux.**

This project uses a local PipeWire/PulseAudio output monitor, the application's *existing* `BridgeAudioCaptureSource` and BPM/dance controllers, and a BepInEx/Harmony plugin. It also pauses dancing while the character speaks and resumes after a configurable delay. It does **not** distribute any Animates or BepInEx binaries.

> **Release status: public beta packaging (v0.3.0).** The **v0.2.0 plugin + v0.2.1 relay/launcher behavior** was tested successfully on one Linux installation. The generalized installer, its new KDE menu entry, and other Linux configurations still need community testing. Report your findings rather than assuming general compatibility.

## What works in the tested setup

- Native Linux Spotify and Elisa playback trigger the character's existing dance animations.
- Pausing music stops dancing; resuming music starts it again.
- Playing a faster local song changes the movement speed (the original BPM detection has limitations).
- The character stops dancing during her own speech and resumes after it ends while music continues.
- The application's microphone, push-to-talk, speech synthesis and AI conversations continue to work.
- An existing KDE/Proton launcher can start the relay automatically and clean it up at exit.

Other players including RMPC are **expected** to work if they play through the selected default output, but have not been separately confirmed in the test report.

## Requirements

1. A **working installation of Windows Animates** under Proton or Wine, with its existing launcher. This add-on does not install or support the base Windows app, fix WebView2, or configure GPU transparency.
2. **BepInEx 5.4.23.5 Windows x64 Mono**, extracted into the directory containing `Animates.exe`, including `winhttp.dll`, `doorstop_config.ini` and `BepInEx/core`. The wrapper sets `WINEDLLOVERRIDES=winhttp=n,b` unless one is already provided.
3. Linux PipeWire with PulseAudio compatibility (or compatible PulseAudio server), `pactl`, `parec`, Python 3 and output monitor sources.
4. The **Mono C# compiler and disassembler** (`mcs`, `monodis`) because the plugin builds against your **own legally installed** game's managed assemblies. The current build expects a local `netstandard.dll` version 2.1.0.0.
5. A script that already launches Animates successfully (from the community Linux/Wayland compatibility project or your own script).

On Debian/Ubuntu-derived distributions, dependencies can often be installed with:

```bash
sudo apt install mono-mcs mono-utils pulseaudio-utils python3
```

BepInEx's own [Proton/Wine documentation](https://docs.bepinex.dev/articles/advanced/proton_wine.html) explains why the Windows proxy DLL override is required. Install BepInEx separately; it is not included here.

## Install

**Close Animates first.** Download or clone this repository, then from its root run:

```bash
bash scripts/install.sh
```

The default paths are appropriate for a typical [animates-linux-wayland](https://github.com/animatedbug/animates-linux-wayland) setup using `~/Games/Animates` and `~/animates-linux-wayland/bin/run-animates.sh`. These are **defaults**, not requirements. For a different existing setup:

```bash
bash scripts/install.sh \
  --app '/full/path/to/AnimateApp/current' \
  --launcher '/full/path/to/your-working-animates-launcher.sh'
```

`--app` must name the directory containing `Animates.exe` and `Animates_Data`. `--launcher` must be an existing working script; the installer never replaces it.

The installer compiles the C# plugin locally, keeps timestamped backups of any previous plugin, places the Python relay and wrapper in `~/.local/share/animates-linux-audio-bridge/` (or `$XDG_DATA_HOME`) and creates an **additional** KDE application menu entry called **Animates (Linux Audio Bridge)**. Your original KDE icon remains untouched. If you want to reuse it, change only its Program path to the wrapper printed by the installer.

The relay begins automatically when you use the new entry, and stops when its corresponding launcher process exits. No terminal window is required. Launching the original KDE icon instead will not automatically start the relay unless you explicitly change that entry.

## Configuration

After first running Animates with the plugin, edit:

```text
<Animates install>/BepInEx/config/linuxideas.animates.linuxaudio.cfg
```

- `SuppressDanceWhileSpeaking = true` — suppress dance during Animates' own spoken responses.
- `ResumeDelaySeconds = 2` — seconds before dance can resume after speech (clamped to 0–10 by the plugin).

Restart Animates to apply. The base game's feature flags are not forcibly overridden.

## Troubleshooting

```bash
# BepInEx and plugin diagnostic messages
APP='/full/path/to/AnimateApp/current'
grep -E 'BepInEx|\[LinuxAudio\]' "$APP/BepInEx/LogOutput.log" | tail -n 80

# Background audio relay log
tail -n 80 "${XDG_STATE_HOME:-$HOME/.local/state}/animates-linux-audio-bridge/relay.log"

# Confirm output monitor exists
pactl get-default-sink
pactl list short sources | grep '\.monitor'
```

If BepInEx doesn't load, check `winhttp.dll` and your existing launcher environment. If the relay cannot capture, confirm that `pactl` and `parec` are installed and that your playback device exposes a monitor. If the dance never starts, check whether the game's music activity feature is enabled and whether Animates updated to an incompatible managed API version.

**Audio scope and privacy:** This captures **all sound routed through your default playback device**, not only Spotify or music. Music from browser tabs, video and system alerts can also trigger dancing. Samples are streamed to Animates over **localhost TCP (127.0.0.1:47883)**; this project does not send audio over the Internet or save audio samples to files. Other software running under your local user can potentially access the unencrypted localhost port, so do not expose or forward it. The speech gate suppresses dancing, **not** audio capture.

## Remove / roll back

Close Animates, then:

```bash
bash scripts/uninstall.sh
```

If you installed using a custom `--app` location, set `ANIMATES_APP` when uninstalling:

```bash
ANIMATES_APP='/full/path/to/AnimateApp/current' bash scripts/uninstall.sh
```

This removes the add-on plugin, relay and additional KDE entry. It intentionally preserves your original launcher, BepInEx installation, Proton prefix, configuration and plugin backups in `BepInEx/bridge-backups/`. If you manually changed your original KDE icon, restore its earlier Program path. To restore an older plugin manually, copy the desired DLL backup into `BepInEx/plugins/AnimatesLinuxAudio.dll` after closing Animates.

## Development and compatibility

```bash
bash -n scripts/*.sh
python3 -m unittest discover -s tests -v
```

The CI pipeline checks shell syntax and Python tests. It **cannot compile the C# plugin without the proprietary Animates assemblies**, which are never checked into this repository. Builds happen locally against each user's installation. Community reports should include game build, Proton version, Linux distro, audio server, and sanitized logs (no paths or account tokens).

Tested baseline: **Animates Unity 6000.3.18f1; GE-Proton11-7; PipeWire 1.6.2; BepInEx 5.4.23.5**. This is a single-system success report, not a cross-distribution guarantee.

## Prior work and acknowledgments

- [Animates (Ani) on Linux](https://github.com/psre423p45/Animates-Ani-on-Linux) — earlier Linux compatibility research and fixes.
- [animates-linux-wayland](https://github.com/animatedbug/animates-linux-wayland) — community Wayland/Proton launcher and related compatibility work.
- BepInEx, Harmony, UnityPy, Wine/Proton and PipeWire contributors.

This independent interoperability add-on is **not affiliated with or endorsed by Animation Inc.**, and no claim of being the first-ever private implementation is made. All Animates trademarks, binaries and artwork remain with their respective owners.

## License

The **original add-on source code and scripts only** are released under the [MIT License](LICENSE). Animates, BepInEx and other dependencies retain their own licenses.