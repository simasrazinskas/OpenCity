#!/bin/bash
# Headless OpenCity test run (no window): builds nothing, just runs the game offscreen.
#
# usage: mods/city/tools/autotest.sh [map] [autotest-spec] [support-dir]
#   map           map folder name under mods/city/maps (default: first non-shellmap map),
#                 or "menu" to screenshot the main menu (shellmap) instead of playing a map,
#                 or "replay:<file.orarep>" to play back a recorded game and log the same stats
#   autotest-spec OPENCITY_AUTOTEST value, e.g. "ticks=3000;shots=500,3000;timestep=5;log=250;scenario=basic"
#   support-dir   isolated OpenRA support dir (logs, screenshots). Default: /tmp/opencity-test-<pid>
#
# Prints autotest log lines + any exception logs, and the paths of screenshots taken.
# Screenshots end up in <support-dir>/Screenshots/city/{DEV_VERSION}/*.png (view them with the Read tool).
set -u
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
OPENCITY_AUTOTEST="$SPEC" SDL_VIDEODRIVER=offscreen timeout 600 dotnet bin/OpenRA.dll Engine.EngineDir=".." \
	Engine.SupportDir="$SD" Game.Mod=city "${LAUNCH[@]}" Graphics.Mode=Windowed Graphics.WindowedSize=1600,900 \
	Sound.Engine=Dummy 2>&1 | grep -E "autotest|Exception|exception" | head -20000
for f in "$SD"/Logs/exception*.log; do
	[ -f "$f" ] && { echo "== $f"; grep -v "^\s*at System\." "$f" | head -40; }
done
echo "Screenshots:"
find "$SD"/Screenshots -name "*.png" 2>/dev/null | sort
