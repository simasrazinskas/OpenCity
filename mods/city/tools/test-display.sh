#!/bin/bash
# Real SDL/OpenGL resize and readability smoke test. Requires a built game and an offscreen GL driver.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
RESULTS=$(mktemp -d /tmp/opencity-display-XXXXXX)

verify() {
	local name=$1
	if rg -q 'audit .* [1-9][0-9]* issues|Exception|not found|not handled' "$RESULTS/$name.log"; then
		cat "$RESULTS/$name.log"
		echo "Display check failed; logs and screenshots: $RESULTS"
		exit 1
	fi
	rg -q 'autotest finished' "$RESULTS/$name.log"
	rg 'audit|window:' "$RESULTS/$name.log"
}

OPENCITY_RES=1600,900 OPENCITY_UISCALE=1.25 "$ROOT/mods/city/tools/autotest.sh" green-valley \
	'ticks=1300;shots=100,250,400,550,700,850,1000,1150;shotui=key:CityToolZoning,keep+resize:800x600,keep+resize:1235x777,keep+resize:1600x900,keep+resize:800x450,keep+resize:860x620,keep+resize:1920x1080,keep+resize:800x600;audit=1;timestep=10;log=1300' \
	"$RESULTS/resize" > "$RESULTS/resize.log" 2>&1
verify resize
for size in 800x600 1235x777 1600x900 800x450 860x620 1920x1080; do
	rg -q "window: native=$size," "$RESULTS/resize.log"
done

OPENCITY_RES=1600,900 OPENCITY_UISCALE=1.25 OPENCITY_ARGS=Graphics.Mode=PseudoFullscreen \
	"$ROOT/mods/city/tools/autotest.sh" green-valley \
	'ticks=550;shots=100,250,400;shotui=key:CityToolZoning,keep+windowed+resize:860x620,keep+resize:1600x900;audit=1;timestep=10;log=550' \
	"$RESULTS/fullscreen" > "$RESULTS/fullscreen.log" 2>&1
verify fullscreen
rg -q 'configured mode remains PseudoFullscreen' "$RESULTS/fullscreen.log"
rg -q 'window: native=860x620,' "$RESULTS/fullscreen.log"

OPENCITY_RES=1600,900 OPENCITY_UISCALE=1.25 "$ROOT/mods/city/tools/autotest.sh" green-valley \
	'ticks=2300;shots=100,220,340,460,580,700,820,940,1060,1180,1300,1420,1540,1660,1780,1900,2020,2140;shotui=key:CityToolRoad,key:CityToolZoning,key:CityToolNetworks,key:CityToolIndustry,key:CityToolTransit,key:CityBudget,key:CityStats,key:CityProduction,key:CityPolicies,key:CityProgression,key:CityDistricts,key:CityTiles,key:CityAchievements,key:CityAdvisor,key:CityMap,key:CityNotifications,key:CityInfo,key:CityChirper;audit=1;timestep=8;log=2300' \
	"$RESULTS/menus" > "$RESULTS/menus.log" 2>&1
verify menus
echo "Display checks passed. Logs and screenshots: $RESULTS"
