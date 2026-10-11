#!/usr/bin/env bash
# Local compiler + relay + opt-in KDE launcher; no proprietary files distributed.
set -euo pipefail
HERE="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
APP="${ANIMATES_APP:-$HOME/Games/Animates/prefix/pfx/drive_c/users/steamuser/AppData/Local/AnimateApp/current}"
ORIGINAL="${ANIMATES_LAUNCHER:-$HOME/animates-linux-wayland/bin/run-animates.sh}"

usage() {
    cat <<'HELP'
Usage: bash scripts/install.sh [--app /absolute/path/to/AnimateApp/current]
                               [--launcher /absolute/path/to/existing/launch-script]

Requires an existing functioning Proton launcher and Windows x64 BepInEx 5 Mono.
Builds the plugin locally and adds a separate "Animates (Linux Audio Bridge)" menu entry.
Never replaces your existing launch script or KDE entry.
HELP
}
while (($#)); do
    case "$1" in
        --app|--launcher)
            [[ $# -ge 2 ]] || { echo "Missing argument to $1" >&2; exit 2; }
            if [[ "$1" == --app ]]; then APP="$2"; else ORIGINAL="$2"; fi
            shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
    esac
done
for exe in pactl parec python3; do
    command -v "$exe" >/dev/null 2>&1 || { echo "Missing $exe (on Debian/Ubuntu, install pulseaudio-utils and python3)." >&2; exit 2; }
done
[[ -f "$APP/Animates.exe" ]] || { echo "Animates.exe not found at: $APP (use --app PATH)." >&2; exit 2; }
[[ -f "$ORIGINAL" ]] || { echo "Existing launcher not found: $ORIGINAL (use --launcher PATH)." >&2; exit 2; }
APP="$(cd -- "$APP" && pwd -P)"
ORIGINAL="$(realpath -- "$ORIGINAL")"

# Build first: if the compiler or game API is incompatible, leave KDE and relay untouched.
ANIMATES_APP="$APP" bash "$HERE/scripts/build-plugin.sh"

DATA="${XDG_DATA_HOME:-$HOME/.local/share}/animates-linux-audio-bridge"
STATE="${XDG_STATE_HOME:-$HOME/.local/state}/animates-linux-audio-bridge"
CONF_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/animates-linux-audio-bridge"
DESKTOP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
WRAPPER="$DATA/run-animates-with-audio.sh"
DESKTOP="$DESKTOP_DIR/animates-linux-audio-bridge.desktop"
mkdir -p -- "$DATA" "$STATE" "$CONF_DIR" "$DESKTOP_DIR"
install -m 0644 -- "$HERE/relay/pipewire-relay.py" "$DATA/pipewire-relay.py"
install -m 0755 -- "$HERE/scripts/run-animates-with-audio.sh" "$WRAPPER"
CONFIG="$CONF_DIR/launcher.conf"
if [[ -f "$CONFIG" ]]; then
    cp -p -- "$CONFIG" "$CONFIG.backup.$(date -u +%Y%m%dT%H%M%SZ)"
fi
printf 'ORIGINAL_LAUNCHER=%q\n' "$ORIGINAL" >"$CONFIG"
chmod 600 "$CONFIG"
# Desktop Exec quoting: in standard user paths, quoted executable path is sufficient.
# The installer rejects unusual quoting/metacharacters to avoid broken desktop entries.
case "$WRAPPER" in
    *$'\n'*|*'"'*|*'`'*|*'\\'*|*'$'*|*'%'*)
        echo "Unusual XDG_DATA_HOME path. Wrapper installed at $WRAPPER, but KDE entry was not generated." >&2
        exit 5 ;;
esac
cat >"$DESKTOP" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Animates (Linux Audio Bridge)
Comment=Start Animates with PipeWire music-responsive dancing
Exec="$WRAPPER"
Icon=audio-x-generic
Terminal=false
Categories=AudioVideo;Utility;
DESKTOP
printf '\nInstalled audio relay and launcher:\n  %s\n  %s\n' "$WRAPPER" "$DESKTOP"
printf 'Existing KDE entry and Proton launcher were not changed.\n'
printf 'Open "Animates (Linux Audio Bridge)" from the application menu, or set your existing KDE entry to: %s\n' "$WRAPPER"