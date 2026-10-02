#!/bin/bash
# Simulation golden logs: prove a change is render-only (no synced state changed).
#
# usage: mods/city/tools/golden.sh record|check [name...]
#   record  run the scenarios and store their date=/report lines in mods/city/tests/golden/<name>.txt
#   check   run them again and diff against the stored lines (exit 1 on any difference)
# Scenarios (name -> map + autotest spec) are listed below. Default: all of them.
# The game seed is pinned (OPENRA_RANDOM_SEED, default 12345) so fresh runs are comparable.
# Requires a Release build (make all). Runs headless (offscreen), never opens a window.
set -u
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
declare -A SPEC=(
	[full]="green-valley|ticks=12000;scenario=full;timestep=1"
	[stress]="green-valley|ticks=6000;scenario=stress;timestep=1"
	[automayor]="green-valley|ticks=24000;scenario=automayor;timestep=1"
)
MODE=${1:-check}; shift || true
NAMES=("$@"); [ ${#NAMES[@]} -eq 0 ] && NAMES=(full stress automayor)
FAIL=0
for n in "${NAMES[@]}"; do
	IFS='|' read -r MAP S <<< "${SPEC[$n]}"
	SD=/tmp/opencity-golden-$n-$$
	OPENRA_RANDOM_SEED=${OPENRA_RANDOM_SEED:-12345} "$ROOT/mods/city/tools/autotest.sh" "$MAP" "$S" "$SD" 2>&1 | grep -E "^\[autotest t=" | grep -E "date=|report" \
		| sed 's/^\[autotest t=\([0-9]*\)\] /t=\1 /' > "$SD.txt"
	G="$ROOT/mods/city/tests/golden/$n.txt"
	if [ "$MODE" = record ]; then
		cp "$SD.txt" "$G"; echo "$n: recorded $(wc -l < "$G") lines"
	elif diff -q "$G" "$SD.txt" > /dev/null; then
		echo "$n: GOLDEN OK ($(wc -l < "$G") lines)"
	else
		echo "$n: GOLDEN MISMATCH"; diff "$G" "$SD.txt" | head -${DIFFLINES:-12}; FAIL=1
	fi
	rm -rf "$SD" "$SD.txt"
done
exit $FAIL
