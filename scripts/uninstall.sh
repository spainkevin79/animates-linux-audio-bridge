#!/usr/bin/env bash
# Uninstalls only project-owned files; does not remove backups or BepInEx.
set -euo pipefail
APP="${ANIMATES_APP:-$HOME/Games/Animates/prefix/pfx/drive_c/users/steamuser/AppData/Local/AnimateApp/current}"
if pgrep -u "$(id -u)" -f '[Aa]nimates[.]exe' >/dev/null; then
    echo 'Close Animates before uninstalling.' >&2
    exit 2
fi
rm -f -- "$APP/BepInEx/plugins/AnimatesLinuxAudio.dll"
rm -f -- "${XDG_DATA_HOME:-$HOME/.local/share}/applications/animates-linux-audio-bridge.desktop"
rm -f -- "${XDG_DATA_HOME:-$HOME/.local/share}/animates-linux-audio-bridge/pipewire-relay.py"
rm -f -- "${XDG_DATA_HOME:-$HOME/.local/share}/animates-linux-audio-bridge/run-animates-with-audio.sh"
printf 'Uninstalled plugin, relay and new menu entry.\n'
printf 'Original Animates/Proton launcher and saved plugin backups remain untouched.\n'
printf 'If you manually changed your original KDE icon, restore its original Program path.\n'