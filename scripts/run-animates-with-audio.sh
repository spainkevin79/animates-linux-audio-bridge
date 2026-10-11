#!/usr/bin/env bash
# Runs the unmodified Animates launcher and this project's own audio relay.
set -u
CONFIG="${XDG_CONFIG_HOME:-$HOME/.config}/animates-linux-audio-bridge/launcher.conf"
DATA="${XDG_DATA_HOME:-$HOME/.local/share}/animates-linux-audio-bridge"
STATE="${XDG_STATE_HOME:-$HOME/.local/state}/animates-linux-audio-bridge"
RELAY="$DATA/pipewire-relay.py"
LOG="$STATE/relay.log"

[[ -r "$CONFIG" ]] || { printf 'Missing bridge configuration: %s\n' "$CONFIG" >&2; exit 1; }
# Only the local installer writes this file. Contains the original launcher's quoted path.
# shellcheck source=/dev/null
source "$CONFIG"
[[ -f "$ORIGINAL_LAUNCHER" ]] || { echo "Missing original launcher: $ORIGINAL_LAUNCHER" >&2; exit 1; }
mkdir -p -- "$STATE"

# Required for BepInEx to load under Wine. Preserve existing user overrides.
case ";${WINEDLLOVERRIDES-};" in
    *';winhttp='*) ;;
    *) export WINEDLLOVERRIDES="winhttp=n,b${WINEDLLOVERRIDES:+;$WINEDLLOVERRIDES}" ;;
esac

relay_pid=''
cleanup() {
    if [[ -n "$relay_pid" ]]; then
        kill -TERM "$relay_pid" 2>/dev/null || true
        wait "$relay_pid" 2>/dev/null || true
    fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

if [[ -f "$RELAY" ]] && command -v python3 >/dev/null 2>&1; then
    # Limit log growth on repeated launches.
    if [[ -f "$LOG" ]] && [[ "$(stat -c %s "$LOG" 2>/dev/null || echo 0)" -gt 2097152 ]]; then
        mv -f -- "$LOG" "$LOG.old"
    fi
    python3 -u "$RELAY" >>"$LOG" 2>&1 &
    relay_pid=$!
else
    echo "[launcher] No Python relay at $RELAY; launching Animates without music detection." >>"$LOG"
fi

# All Proton/Wayland/WebView2 settings remain owned by the original launcher.
"$ORIGINAL_LAUNCHER" "$@"