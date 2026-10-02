# CIVIC inventory: every placeable non-zoned building

Sources: `rules/{services,industry,transit,construction}.yaml`, `sequences/{services,industry,transit}.yaml`,
`tools/gen_svc*.py`, `gen_ind.py`, `gen_pt*.py`. Groups follow the build toolbar order
(`CityToolbarLogic.cs`): power, water, police, fire, health, education, parks, garbage, deathcare, comms, admin,
industry, transit. Footprint is `Building.Dimensions` (x by y cells). Upgrades are `ServiceUpgrade@<id>`
(no art today; designed here as extensions drawn inside the same footprint).

## Common states (every building unless marked otherwise)
| state | meaning | how it looks |
|---|---|---|
| day | operating, default facing (+Y, front toward the viewer's lower left) | lit by the fixed light |
| night | operating at night | lit windows, lamps, beacons; ground dimmed |
| winter | snow season | snow on roofs and ground, trees bare |
| inactive | no power or no water (CityStatusIcons) | windows dark, machines and smoke stopped, dimmed |
| construction | being built | 2 stages: foundations + scaffolding, then shell under scaffolding with crane |
| burnt | destroyed by fire | charred shell, black roofs |
| rubble | `rubble` actor, one per footprint cell | shared 1x1 debris tiles (4 variants) |
| facings | buildings with a clear front | 4 facings: front toward +Y, +X, -Y, -X |
| upgrades | per `ServiceUpgrade` | base, each upgrade, all upgrades |

## Power (`power`)
| actor | fp | anim | special states |
|---|---|---|---|
| windturbine | 1x1 | rotor, 4 frames | stopped when inactive (no wind) |
| powerplant-coal | 2x2 | chimney smoke, 4 frames | coal heap, conveyor; no smoke when inactive |
| solarplant | 2x2 | - | panel rows; winter = snow on panels |
| powerplant-gas | 3x3 | stack exhaust, 4 frames | turbine hall, 2 stacks, tanks |
| hydro-dam | 3x3 | spillway water, 4 frames | dam wall across a river, reservoir side, spillway |
| powerplant-nuclear | 3x3 | cooling-tower steam, 4 frames | 2 cooling towers, reactor dome |
| transformer, battery | - | - | owned by NET (network buildings in networks.yaml) |

## Water (`water`)
| actor | fp | anim | special states |
|---|---|---|---|
| watertower | 1x1 | - | tank on legs; night beacon |
| waterpump | 1x1 | - | pump house with intake pipe |
| waterpump-large | 2x2 | - | pump hall, settling basins |
| sewage-outlet, treatment-plant | - | - | owned by NET |

## Police (`police`)
| actor | fp | upgrades | notes |
|---|---|---|---|
| policebox | 1x1 | - | blue kiosk with lamp |
| police | 2x2 | garage, cells | station, patrol cars in the lot, flag (anim 4); facings |
| police-hq | 3x3 | garage, cells | tall HQ block, roof helipad, car park |
| prison | 3x3 | cells | walls, watchtowers, cell blocks, exercise yard |

## Fire (`fire`)
| actor | fp | upgrades | notes |
|---|---|---|---|
| firehouse | 1x1 | - | small one-bay hall, hose tower; facings |
| firestation | 2x2 | bay | red open bays with engines, drill tower; facings |
| fire-helipad | 2x2 | - | H pad, hangar, red helicopter |
| firewatch | 1x1 | - | lookout tower in pines |

## Health (`health`)
| actor | fp | upgrades | notes |
|---|---|---|---|
| clinic | 2x2 | depot, wing | small clinic with red cross; facings |
| hospital | 3x3 | trauma, depot | big hospital, red cross, ambulance bays, roof helipad |
| medevac-helipad | 2x2 | - | H pad with red cross, ambulance helicopter |

## Education (`education`)
| actor | fp | upgrades | notes |
|---|---|---|---|
| school | 2x2 | wing, library | brick school, playground; facings |
| highschool | 3x3 | wing, library | larger school, running track |
| college | 3x3 | wing, library | campus blocks, quad |
| university | 3x3 | wing, library | domed hall, clock tower, lawns |

## Parks (`parks`)
| actor | fp | anim | notes |
|---|---|---|---|
| park-small | 1x1 | - | paths, benches, trees, flower beds |
| plaza | 1x1 | fountain, 4 frames | paved plaza with fountain |
| park-large | 2x2 | fountain, 4 frames | playground, pond, gazebo, trees |
| sportsfield | 2x2 | - | pitch with goals, stands, floodlights (night) |

## Garbage (`garbage`)
| actor | fp | upgrades | special states |
|---|---|---|---|
| landfill | 3x3 | depot, recycling | fill levels 0/25/50/75/100% |
| incinerator | 3x3 | furnace, filter | chimney smoke (anim 4) |
| recycling | 3x3 | depot | sorting hall, bale stacks, conveyors |

## Deathcare (`deathcare`)
| actor | fp | upgrades | special states |
|---|---|---|---|
| cemetery | 3x3 | columbarium | fill levels 0/25/50/75/100% (graves), chapel |
| crematorium | 2x2 | garage | chimney wisp (anim 4); facings |

## Comms (`comms`)
| actor | fp | upgrades | notes |
|---|---|---|---|
| postoffice | 2x2 | fleet | post office with yellow vans; facings |
| sortingcenter | 3x3 | - | big shed, loading docks, trucks |
| telecom-mast | 1x1 | - | lattice mast, red beacon (blinks at night: 2 frames) |
| telecom-tower | 2x2 | - | concrete TV tower with pod, beacon (2 frames) |

## Admin (`admin`)
| actor | fp | notes |
|---|---|---|
| cityhall | 3x3 | columned hall, dome, flag (anim 4), fountain forecourt; facings |
| welfare | 2x2 | office with canopy and benches; facings |

## Industry (`industry`): hubs + extractor areas
| actor | fp | notes |
|---|---|---|
| farm-hub | 3x3 | farmhouse, red barn, silos, tractor |
| forestry-hub | 2x2 | sawmill shed, log piles |
| quarry-hub | 3x3 | crusher, conveyor, gravel heaps |
| mine-hub | 3x3 | headframe with wheel (anim 4), spoil heap |
| oil-hub | 2x2 | tanks, refinery column, gas flare (anim 4) |
| fish-hub | 2x2 | pier, fish hall, boat |
| oil-derrick | 1x1 | pumpjack, 4 frames (placed by the oil hub in its area) |

Extractor area tiles (1x1, `extractor-area` sequence, 4 variants each today: farm-grain, farm-vegetables,
farm-livestock, farm-cotton, quarry, mine, oil, fish). Designed:
- grain, vegetables, cotton: 4 growth stages (ploughed, sprouting, growing, ripe) + winter fallow
- orchard: 4 stages (new, optional alternative crop)
- livestock: pasture with fences, cows and sheep, 4 variants
- forestry (today draws nothing): plantation saplings, young, mature, cut stumps
- quarry: pit tiles at 4 depths; mine: spoil/gravel tiles; oil: pad tiles; fish: water with nets and buoys

## Transit (`transit`)
| actor | fp | notes |
|---|---|---|
| busdepot | 3x2 | garage hall with buses, forecourt; facings |
| taxidepot | 2x2 | office, yellow taxis in bays; facings |
| metrostation | 1x2 | entrance kiosk with stairs, roundel; facings |
| tramdepot | 3x2 | hall with tracks and trams |
| trainstation | 2x4 | platforms, canopy, station building, clock |
| railyard | 3x2 | sidings, wagons, signal box |
| cargoharbor | 3x3 | quay, gantry crane (anim 4), containers, ship |
| cargoairport | 3x3 | runway, hangar, control tower, plane |

## Outside connections (`construction.yaml`)
| actor | notes |
|---|---|
| highway-w/e/n/s | highway entry: overhead gantry with "OpenCity" welcome sign, 4 directions |

## Not CIVIC
- svc-fire flames, svc-heli, svc-pile garbage heaps, all vehicles: LIFE.
- transformer, battery, sewage-outlet, treatment-plant, transit stops, tracks: NET.

## Counts
40 services actors (incl. rubble), 7 industry actors + 8 area kinds (+ forestry, orchard), 8 transit buildings,
4 highway entries (one model, 4 facings).

## Designed (generator `tools/iso_civic.py`, models `tools/iso_civic_m_*.py`)
65 models: power 6, water 3, police 4, fire 4, health 3, education 4, parks 4, garbage 3, deathcare 2,
comms 4, admin 2, industry hubs 7, extractor area tiles 10, transit 8, highway entry 1; plus 4 rubble tiles.
745 PNGs in the Figma manifest (67 groups, one row per building).

Row per building: day, [variants / growth stages], night, lit layer (emissive-only companion, same anchor),
winter, no power (dimmed, machines stopped), anim frames, fill levels, upgrades (each + all), facings +X/-Y/-X,
construction 1 and 2, burnt. Anim and facing strips (anchor-aligned) are in the last group.

## Look decisions (others should match)
- Lots: services sit on their own lot ground (grass / paving / concrete) filling the footprint; no drop shadows.
- Trees in lots are the kit species (`isokit.trees`) scaled to size, so they match KIT's street trees and seasons.
- Natural water (harbour berth, dam river, fish hub, fishing tiles) at z = -5 px with quay walls down to it.
- Category accents: health white + teal + glowing red cross; fire red brick + white; police slate blue + white
  signage (3x5 pixel lettering "POLICE", "FIRE", "PRISON"); education brick + terracotta roofs; comms purple;
  admin sandstone + gold dome; garbage greens/browns; deathcare grey stone, dark hedges, cypresses; power
  red/white striped stacks, white steam; transit teal trim.
- Parked vehicles follow the kit's reference car (2 px wheels, 4 px body, 3 px cabin); service vehicles are
  boxes in their livery. Aircraft, ships and helicopters are LIFE's: pads, runways and berths are left empty.
- Inactive = desaturated and darkened, no smoke/steam, rotors stopped. Burnt = soot recolour with embers.
- Construction is a shared per-footprint site (stage 1 foundations, stage 2 concrete frame + scaffolding +
  tower crane), its height scaled to the finished building.

