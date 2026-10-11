#!/usr/bin/env bash
# Compile the integration against the user's legally installed Animates assemblies.
set -euo pipefail
HERE="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
APP="${ANIMATES_APP:-$HOME/Games/Animates/prefix/pfx/drive_c/users/steamuser/AppData/Local/AnimateApp/current}"
MANAGED="$APP/Animates_Data/Managed"
OUT="$APP/BepInEx/plugins/AnimatesLinuxAudio.dll"
BACKUP_DIR="$APP/BepInEx/bridge-backups"

for cmd in mcs monodis; do
    command -v "$cmd" >/dev/null 2>&1 || { echo "Missing $cmd (install mono-mcs mono-utils)." >&2; exit 2; }
done
for dll in \
    "$APP/BepInEx/core/BepInEx.dll" \
    "$APP/BepInEx/core/0Harmony.dll" \
    "$MANAGED/Assembly-CSharp.dll" \
    "$MANAGED/UnityEngine.dll" \
    "$MANAGED/UnityEngine.CoreModule.dll" \
    "$MANAGED/netstandard.dll"; do
    [[ -f "$dll" ]] || { echo "Required local assembly missing: $dll" >&2; exit 3; }
done
[[ -f "$APP/winhttp.dll" ]] || { echo 'Missing BepInEx winhttp.dll proxy; install BepInEx Windows x64 Mono in the game directory first.' >&2; exit 3; }
VERSION="$(monodis --assembly "$MANAGED/netstandard.dll" 2>/dev/null | awk '$1 == "Version:" { version = $2 } END { print version }')"
[[ "$VERSION" == "2.1.0.0" ]] || { echo "Requires the game's netstandard 2.1.0.0; found '${VERSION:-unknown}'." >&2; exit 3; }
if pgrep -u "$(id -u)" -f '[Aa]nimates[.]exe' >/dev/null; then
    echo 'Close Animates before installing or upgrading this plugin.' >&2
    exit 4
fi
TMP="$(mktemp -d)"
trap 'rm -rf -- "$TMP"' EXIT
printf 'Compiling against Animates at: %s\n' "$APP"
mcs -target:library -out:"$TMP/AnimatesLinuxAudio.dll" \
    -r:"$MANAGED/netstandard.dll" \
    -r:"$APP/BepInEx/core/BepInEx.dll" \
    -r:"$APP/BepInEx/core/0Harmony.dll" \
    -r:"$MANAGED/Assembly-CSharp.dll" \
    -r:"$MANAGED/UnityEngine.dll" \
    -r:"$MANAGED/UnityEngine.CoreModule.dll" \
    "$HERE/plugin/AnimatesLinuxAudio.cs"
[[ -s "$TMP/AnimatesLinuxAudio.dll" ]] || { echo 'Compiler did not produce the DLL.' >&2; exit 5; }
mkdir -p -- "$(dirname "$OUT")" "$BACKUP_DIR"
if [[ -f "$OUT" ]] && ! cmp -s "$OUT" "$TMP/AnimatesLinuxAudio.dll"; then
    stamp="$(date -u +%Y%m%dT%H%M%SZ)"
    backup="$BACKUP_DIR/AnimatesLinuxAudio.$stamp.dll"
    cp -p -- "$OUT" "$backup"
    printf 'Preserved old plugin at: %s\n' "$backup"
fi
cp -- "$TMP/AnimatesLinuxAudio.dll" "$OUT.new"
mv -f -- "$OUT.new" "$OUT"
printf 'Installed plugin: %s\n' "$OUT"