#!/bin/bash
# Headless OpenCity test run (no window): builds nothing, just runs the game offscreen.
#
# usage: mods/city/tools/autotest.sh [map] [autotest-spec] [support-dir]
#   map           map folder name under mods/city/maps (default: first non-shellmap map),
#                 or "menu" to screenshot the main menu (shellmap) instead of playing a map,
#                 or "replay:<file.orarep>" to play back a recorded game and log the same stats
#   autotest-spec OPENCITY_AUTOTEST value, e.g. "ticks=3000;shots=500,3000;timestep=5;log=250;scenario=basic"
#                 For normal gameplay recordings, use replaytimestep=1 instead of timestep=1 to preserve game speed.
#   support-dir   isolated OpenRA support dir (logs, screenshots). Default: /tmp/opencity-test-<pid>
#
# Prints autotest log lines + any exception logs, and the paths of screenshots taken.
# shotui also accepts resize:800x600 and windowed (leave fullscreen without changing Graphics.Mode).
# Prefix an action with keep+ to retain open panels while resizing.
# Example: shots=100,250,400;shotui=key:CityToolZoning,keep+windowed+resize:800x600,keep+resize:1600x900
# Screenshots end up in <support-dir>/Screenshots/city/{DEV_VERSION}/*.png (view them with the Read tool).
#
# Environment overrides (to check the UI at other sizes):
#   OPENCITY_RES=1920,1080   window size (default 1600,900)
#   OPENCITY_UISCALE=1.5     Graphics.UIScale (default: the engine default)
#   OPENCITY_ARGS="..."      any extra launch arguments (e.g. "Graphics.ViewportDistance=Far")
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
MAP=${1:-}
SPEC=${2:-"ticks=1500;shots=300,1500;timestep=10;log=250"}
SD=${3:-/tmp/opencity-test-$$}
if [ -z "$MAP" ]; then
	MAP=$(grep -l "Visibility: Lobby" "$ROOT"/mods/city/maps/*/map.yaml | head -1 | xargs dirname | xargs basename)
fi
rm -rf "$SD" && mkdir -p "$SD"
cd "$ROOT"
LAUNCH=(Launch.Map="$MAP")
if [ "$MAP" = "menu" ]; then
	LAUNCH=()
	SPEC="$SPEC;menu=1"
elif [[ "$MAP" == replay:* ]]; then
	# Play back a recorded replay (e.g. from a previous run's <support-dir>/Replays) to check determinism.
	LAUNCH=(Launch.Replay="${MAP#replay:}")
fi
RUN_STATUS=0
OPENCITY_AUTOTEST="$SPEC" SDL_VIDEODRIVER=offscreen timeout "${OPENCITY_TIMEOUT:-600}" dotnet bin/OpenRA.dll Engine.EngineDir=".." \
	Engine.SupportDir="$SD" Game.Mod=city "${LAUNCH[@]}" Graphics.Mode=Windowed Graphics.WindowedSize="${OPENCITY_RES:-1600,900}" \
	${OPENCITY_UISCALE:+Graphics.UIScale=$OPENCITY_UISCALE} ${OPENCITY_ARGS:-} Sound.Engine=Dummy 2>&1 | grep -E "autotest|Exception|exception" || RUN_STATUS=$?
for f in "$SD"/Logs/exception*.log; do
	[ -f "$f" ] && { RUN_STATUS=1; echo "== $f"; grep -v "^\s*at System\." "$f" | head -40; }
done
for f in "$SD"/Logs/syncreport*.log; do
	[ -f "$f" ] && { RUN_STATUS=1; echo "Replay synchronization failed: $f"; }
done
echo "Screenshots:"
find "$SD"/Screenshots -name "*.png" 2>/dev/null | sort

exit "$RUN_STATUS"
