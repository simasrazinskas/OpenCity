## Owned by the environment work package. See mods/city/ARCHITECTURE.md.

## Chirper messages. `$arg` is the optional argument passed to ICityStatistics.Chirp.
chirp-milestone-1 = Our little town just became a { $arg }! Proud of this place.
chirp-milestone-2 = Look at us: { $arg } status unlocked. The mayor is on a roll.
chirp-population-1 = The city now has more than { $arg } residents. Hello, neighbours!
chirp-population-2 = { $arg } residents and counting. It is getting crowded in here.
chirp-power-shortage-1 = Lights are flickering again. The city is short on electricity by { $arg }%.
chirp-power-shortage-2 = Not enough power for everyone ({ $arg }% short). Anyone have a candle?
chirp-water-shortage-1 = The taps are running dry. Water supply is { $arg }% short.
chirp-water-shortage-2 = Water pressure is terrible today, { $arg }% short of demand.
chirp-broke-1 = The city is out of money. Maybe fewer parades, Mayor?
chirp-broke-2 = Overdrawn again. Someone check the budget!
chirp-snow-1 = It is snowing! Time to dig out the sleds.
chirp-snow-2 = First snow of the season. The whole city looks like a postcard.
chirp-storm-1 = Thunder over the city. Everyone stay inside.
chirp-storm-2 = A proper storm is rolling in. Hold on to your hats!
chirp-heat-1 = { $arg } degrees! The power meters are spinning.
chirp-heat-2 = Heat wave: { $arg } degrees. Air conditioning everywhere.
chirp-cold-1 = { $arg } degrees outside. The heating bill is going to hurt.
chirp-cold-2 = Freezing cold at { $arg } degrees. Power demand is through the roof.

chirp-problem-nopower-1 = { $arg } buildings have no electricity. This is getting ridiculous.
chirp-problem-nopower-2 = Blackout in { $arg } buildings! Do we even have a power plant?
chirp-problem-nowater-1 = { $arg } buildings are without running water. Please fix the pipes!
chirp-problem-nowater-2 = No water in { $arg } buildings. Brushing teeth with soda today.
chirp-problem-garbage-1 = Garbage is piling up outside { $arg } buildings. The smell is unbearable.
chirp-problem-garbage-2 = Trash everywhere, { $arg } buildings affected. Where are the garbage trucks?
chirp-problem-fire-1 = { $arg } buildings are on fire! Where is the fire department?
chirp-problem-fire-2 = Fire spreading across { $arg } buildings. Everybody stay safe!
chirp-problem-crime-1 = Crime is up: { $arg } buildings report break-ins. Police, please.
chirp-problem-crime-2 = { $arg } crime scenes around town. Lock your doors, folks.
chirp-problem-abandoned-1 = { $arg } buildings stand abandoned. Ghost town vibes.
chirp-problem-abandoned-2 = Another empty building. That makes { $arg } abandoned in total.
chirp-problem-noroad-1 = { $arg } buildings cannot be reached by road. How do I get home?
chirp-problem-noroad-2 = { $arg } homes have no road access. Time to build a street.
chirp-problem-traffic-1 = Traffic jam at { $arg } places. I will be late again.
chirp-problem-traffic-2 = Gridlock! { $arg } buildings are stuck in traffic.

## Problem notifications (name shown in tooltips and the alert strip).
problem-nopower = No power
problem-nowater = No running water
problem-nosewage = Sewage backing up
problem-dirtywater = Dirty water
problem-noroad = No road access
problem-abandoned = Abandoned
problem-unhappy = Unhappy citizens
problem-noworkers = Not enough workers
problem-nocustomers = Not enough customers
problem-nogoods = Not enough goods
problem-garbage = Garbage piling up
problem-fire = On fire
problem-crime = Crime
problem-sick = Sick citizens
problem-ambulance = Waiting for an ambulance
problem-highrent = Rent is too high
problem-traffic = Traffic jam
problem-airpollution = Air pollution
problem-groundpollution = Ground pollution
problem-noise = Noise pollution
problem-leveledup = Leveled up
problem-collapsed = Building collapsed
problem-noservice = Missing service
problem-flooded = Flooded

## Problem severity tiers.
tier-minimal = Minor
tier-info = Information
tier-problem = Problem
tier-warning = Warning
tier-major = Major problem
tier-error = Error
tier-fatal = Fatal

## Weather and seasons (for the clock, tooltips and the UI).
weather-clear = Clear
weather-cloudy = Cloudy
weather-rain = Rain
weather-snow = Snow
weather-storm = Storm
season-spring = Spring
season-summer = Summer
season-autumn = Autumn
season-winter = Winter

## Disasters and trees (chirper).
chirp-flood-warning-1 = Flood warning! The rain will not stop and the shores are filling up.
chirp-flood-warning-2 = The river is rising. Low ground near the water is going under.
chirp-flood-damage = { $arg } buildings are standing in flood water. Somebody get a pump!
chirp-lightning-strike = Lightning struck a building downtown. Thankfully nothing burned.
chirp-lightning-fire = A lightning bolt set a building on fire! Fire department, please hurry!
chirp-trees-dying = The trees near the industry are dying from the pollution. So sad.

## Advisor hints: advisor-<id> is the message ($arg is a number where it makes sense), advisor-<id>-hint says what to do.
advisor-first-steps = Welcome, Mayor! Your city is empty.
advisor-first-steps-hint = Build a road, place a power plant and a water pump, then paint residential, commercial and industrial zones along the road.
advisor-no-power = { $arg } buildings have no electricity and the city produces none.
advisor-no-power-hint = Build a power plant and connect it with power lines or place it next to the buildings.
advisor-blackout = { $arg } buildings are without power.
advisor-blackout-hint = Connect them to the electricity network or add more generation.
advisor-no-water = { $arg } buildings have no water and the city has no supply.
advisor-no-water-hint = Build a water pump or water tower and lay pipes along the roads.
advisor-water-outage = { $arg } buildings have no running water.
advisor-water-outage-hint = Extend the water pipes to them or build another pump.
advisor-power-shortage = The city is { $arg }% short of electricity.
advisor-power-shortage-hint = Build another power plant before buildings start losing power.
advisor-winter-power = Cold weather pushes electricity demand to { $arg }% of normal.
advisor-winter-power-hint = Add generation before winter or build wind and solar capacity.
advisor-water-shortage = The city is { $arg }% short of water.
advisor-water-shortage-hint = Build another pump or tower.
advisor-summer-water = Hot weather pushes water demand to { $arg }% of normal.
advisor-summer-water-hint = Make sure the water supply has some spare capacity for summer.
advisor-unemployment = Unemployment is at { $arg }% of the workforce.
advisor-unemployment-hint = Zone more commercial, industrial or office areas to create jobs.
advisor-labor-shortage = There are { $arg } more jobs than workers.
advisor-labor-shortage-hint = Zone more residential areas so new citizens can move in.
advisor-broke = The city is out of money.
advisor-broke-hint = Raise taxes, cut service budgets or take out a loan to unblock construction.
advisor-deficit = The city loses { $arg } every month.
advisor-deficit-hint = Raise taxes, reduce service budgets or grow the tax base.
advisor-no-road = { $arg } buildings cannot be reached by road.
advisor-no-road-hint = Connect them to the road network.
advisor-garbage = Garbage is piling up at { $arg } buildings.
advisor-garbage-hint = Build a landfill or incinerator and make sure the roads are not jammed.
advisor-crime = Crime is a problem at { $arg } buildings.
advisor-crime-hint = Build a police station near the affected area.
advisor-fire = { $arg } buildings are on fire.
advisor-fire-hint = Build a fire station so the engines arrive faster.
advisor-sick = Sick citizens are waiting for care at { $arg } buildings.
advisor-sick-hint = Build a clinic or hospital.
advisor-abandoned = { $arg } buildings stand abandoned.
advisor-abandoned-hint = Fix the missing service or demolish the building to make room for new growth.
advisor-traffic = Traffic is congested.
advisor-traffic-hint = Upgrade busy roads, add junction control or public transport.
advisor-pollution = Pollution exposure reaches { $arg } in residential areas.
advisor-pollution-hint = Move industry away from homes, plant trees or use cleaner power.
advisor-homeless = { $arg } citizens have no home.
advisor-homeless-hint = Zone more residential areas.
advisor-flood = { $arg } buildings are flooded.
advisor-flood-hint = Wait for the rain to stop and avoid building on the shore.
advisor-zone-residential = Residential demand is high ({ $arg }).
advisor-zone-residential-hint = Zone more residential areas.
advisor-zone-commercial = Commercial demand is high ({ $arg }).
advisor-zone-commercial-hint = Zone more commercial areas.
advisor-zone-industrial = Industrial demand is high ({ $arg }).
advisor-zone-industrial-hint = Zone more industrial areas.
