## Public transport. Owned by the transit work package. See mods/city/ARCHITECTURE.md.

actor-busdepot =
    .name = Bus Depot
    .description = Garage for 12 buses. Needed before bus lines can run.
actor-taxidepot =
    .name = Taxi Depot
    .description = Garage for 8 taxis. Taxis wait at taxi stands.

notification-transit-no-money = Not enough money for this transit project.
notification-transit-not-road = Transit stops must be placed on a road.
notification-transit-stop-exists = There is already a stop here.
notification-transit-no-depot = Build a depot of this type first.
notification-transit-bad-line = A line needs at least two different stops of its type.
notification-transit-no-stop = That stop no longer exists.
notification-transit-not-supported = This transit type is not available yet.
notification-transit-not-owned = You do not own this part of the map yet.
notification-transit-line-cut = A transit line is cut off: a road is missing.

actor-metrostation =
    .name = Metro Station
    .description = An underground station. Link stations with a metro line; tunnels cost money per cell.

label-transit-fleet = Fleet
label-transit-no-road = No road access
label-transit-waiting = Waiting passengers

notification-transit-needs-track = Tram stops must be placed on tram track.
notification-transit-track-needs-road = Tram track can only be laid on roads.
notification-transit-rail-blocked = Something is in the way of the rail.
notification-transit-rail-needs-crossing = Rail can only cross a straight road, at right angles.
notification-transit-rail-terrain = Rail cannot be built on this terrain.

notification-transit-locked = This transit type has not been unlocked yet.

actor-tramdepot =
    .name = Tram Depot
    .description = Garage for 8 trams. Trams run on tram track laid over roads.
actor-trainstation =
    .name = Train Station
    .description = A passenger station. Build it next to rail; trains stop on the track beside it.
actor-railyard =
    .name = Rail Cargo Terminal
    .description = A warehouse with a rail link to the outside world. Connect its track to the map edge.
actor-cargoharbor =
    .name = Cargo Harbor
    .description = A warehouse with a shipping link to the outside world. Must be built next to water.
actor-cargoairport =
    .name = Cargo Airport
    .description = A small, expensive but very fast freight link to the outside world.
