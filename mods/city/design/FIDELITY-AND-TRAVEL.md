# Rendering, window resizing and daily travel

The city UI uses antialiased FreeSans at the actual display scale. Build cards have readable
two-line names and explicit locked badges. Achievements have wider cards; dense labels, chat
wrapping and scroll panels reflow when the window or DPI changes.

The renderer filters RGBA and indexed sprites correctly at fractional scales, handles mirrored
and rotated sprites, filters minification, and clamps sampling to the sprite's atlas bounds.
Native and integer-size pixel artwork stays crisp. This improves rendering of the existing
artwork; it does not replace the source sprites with higher-resolution assets.

SDL window and drawable dimensions are refreshed even when a compositor changes fullscreen
state outside the game. Framebuffers, scissor coordinates, the camera viewport and UI layout
follow the real dimensions. Small windows temporarily fit the UI; growing the window restores
the requested scale. Retired font/chrome atlases are reclaimed during repeated scale changes.

The calendar is **120 real minutes per complete day at 1x**: 7,500 ticks/hour, 180,000 ticks/day,
40 ms/tick. A new city still starts at 07:00. The existing day/night = calendar-month convention
is unchanged. Physical movement is separate from calendar compression: a cell is 16 m,
pedestrians walk at 5 km/h and ordinary cars travel at 40 km/h before road-specific limits,
traffic, junctions and vehicle-type modifiers. Fast-forward speeds the whole simulation.

Citizen schedules use absolute departure/deadline times and route-time estimates. They include
work, school, shopping and leisure, allow stops after work, respect activity durations, and
return home from the actual destination. Walking follows connected sidewalks, including both
directions beside one-way streets. Failed trips do not grant shopping or leisure benefits.
Search-budget deferral is distinct from an unreachable route. Transit access walks use the
same routed trip service.

Businesses reserve incoming goods when a shipment is accepted, receive usable stock on arrival,
and settle failed deliveries once cargo returns. Procurement considers reachable local suppliers
before imports; exports use surplus remaining after local demand. Sampled trucks are physically
simulated; other shipments retain the scalable model with road-route travel times and occupied
fleet slots. Aggregate decorative commuters are disabled when the citizen simulation is present.

**Sandbox** is a checkbox in **New City**. It enables unlimited municipal money, all development
nodes, build/policy permissions and map tiles. The HUD displays infinity and the budget shows
unlimited money. Normal games default to off. The setting travels with the normal serialized
lobby options in saves and replays. One-per-city signature limits and physical placement/network
requirements still apply.

## Regression checks

Run from the repository root after building:

```sh
dotnet build OpenRA.slnx -c Release
dotnet test OpenRA.Test/OpenRA.Test.csproj -c Release --no-build
./utility.sh city --check-yaml
bash mods/city/tools/test-display.sh
python3 mods/city/tools/test-render-filter.py
python3 mods/city/tools/test-render-filter.py es
```

The display test exercises open windows at 800×600 through 1920×1080, fractional scales, an
external fullscreen-to-windowed transition, and the main tool/information panels. It retains
screenshots and logs under a unique `/tmp/opencity-display-*` directory. The shader tests require
NumPy, PyOpenGL and an EGL driver. They cover desktop GLSL 140 and GLES 300.

For simulation checks, use a pinned seed and allow at least 180,000 ticks to cross a complete
daily schedule. `autotest.sh`'s accelerated `timestep=1` is a testing override; it does not change
the game's 1x defaults. A `freight` scenario extends the ordinary full-city scenario using real
construction and farm-product orders and reports local shipment stock changes. Sandbox must be
selected through New City, as in normal play; the harness does not grant money or stock.

For recordings from normal gameplay, accelerate playback with `replaytimestep=1` and omit
`timestep`: playback speed must not override the recorded simulation speed. Recordings created
with the harness's `timestep` override require the same override when replayed. Sync failures
stop replay testing and make `autotest.sh` fail.

Saves and replays are order histories. Simulation changes require fresh regression recordings;
recordings made with the earlier clock and routing rules are not equivalent to new runs.
