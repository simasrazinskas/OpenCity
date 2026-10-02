# OpenCity UI inventory (input for the RCT2-style UI redesign)

Sources: mods/city/chrome*.yaml, mods/city/chrome/*.yaml, OpenRA.Mods.City/Widgets/**, OpenRA.Mods.City/UIArt/**,
mods/city/bits/chrome/cityicons.vec (ground truth for icons), mods/city/tools/ui*.py, mods/city/rules/*.yaml, mods/city/fluent/*.ftl.
All chrome (panels, buttons, order tiles, sidebar art) is generated at runtime by CityChromeGenerator from uistyle.yaml (Dune 2000 brown/red/gold palette).
Fonts (mod.yaml): Tiny, TinyBold, Small, Regular, Bold, MediumBold, BigBold, Title (pixel fonts, OpenCityPixel*).

## 1. Screens

| Screen | Id / file | Notes |
|---|---|---|
| Load screen | CityLoadScreen (UIArt/CityLoadScreen.cs), logo from CityChromeGenerator.Logo.cs | boot splash with logo |
| Main menu | MAINMENU, chrome/mainmenu.yaml, CityMainMenuLogic | TITLE_BLOCK (logo `logos/logo`, tagline), VERSION_LABEL, MENUS: MAIN_MENU (NEW_CITY_BUTTON, LOAD_CITY_BUTTON, SETTINGS_BUTTON, EXTRAS_BUTTON, QUIT_BUTTON), EXTRAS_MENU (REPLAYS_BUTTON, ASSETBROWSER_BUTTON, CREDITS_BUTTON, BACK_BUTTON). Runs over the shellmap |
| New city | CITY_NEWGAME_PANEL, chrome/city-panels.yaml, CityNewGameLogic | 820x520 Background: MAP_LIST (ScrollPanel, MAP_TEMPLATE: TITLE, SIZE), PREVIEW_BG + MapPreview PREVIEW, MAP_TITLE/INFO/AUTHOR, NO_MAPS, BACK_BUTTON, START_BUTTON. No options (difficulty, seed, disasters). Maps: green-valley, lakeside, riverbend |
| Load city | common load-game-browser.yaml / gamesave-browser.yaml | generic chrome only |
| Game-save loading | GAMESAVE_LOADING_SCREEN, chrome/gamesave-loading.yaml | STRIPE (`loadscreen-stripe`), LOGO, TITLE, ProgressBar PROGRESS, DESC |
| In-game HUD | PLAYER_WIDGETS, chrome/ingame-player.yaml | section 2 |
| Observer HUD | OBSERVER_WIDGETS, chrome/ingame-observer.yaml | chat root + OPTIONS_BUTTON "Menu" only |
| Transient messages | TRANSIENTS_PANEL, chrome/ingame-transients.yaml | TextNotificationsDisplay (4 s, 5 lines); line templates in text-notifications.yaml: CHAT_LINE_TEMPLATE, SYSTEM_LINE_TEMPLATE, TRANSIENT_LINE_TEMPLATE (CityTextBackdrop) |
| Pause / game menu | INGAME_MENU (common/ingame-menu.yaml, IngameMenuLogic) | Esc / OPTIONS_BUTTON. Buttons: Resume, Save game, Load game, Settings, Music, Restart, Surrender, Abort mission (editor entries unused). Generic common widgets |
| Settings | SETTINGS_PANEL (common/settings.yaml) tabs: DISPLAY_PANEL, AUDIO_PANEL, INPUT_PANEL, HOTKEYS_PANEL, GAMEPLAY_PANEL, ADVANCED_PANEL | Display is overridden in chrome/settings-display.yaml (battlefield camera dropdown, target lines dropdown, UI_SCALE_SLIDER, status bars dropdown, cursor-double / stance-colours / UI-feedback / transients / pause-shellmap checkboxes, video mode dropdown, window size TextFields + preset dropdown, display selection, frame-limit checkbox + slider, vsync, GL profile). Others common. HOTKEYS lists mods/city/hotkeys.yaml entries |
| Music player | common/musicplayer.yaml | from pause menu; Music list empty in city |
| Replay browser | REPLAYBROWSER_PANEL (common/replaybrowser.yaml) | Extras menu |
| Asset browser | ASSETBROWSER_PANEL (common/assetbrowser.yaml) | Extras menu |
| Credits | CREDITS_PANEL (common/credits.yaml) | Extras menu |
| Confirmation dialogs | common/confirmation-dialogs.yaml, mainmenu-prompts.yaml | quit/abort/restart/surrender, overwrite save |
| Save game | common gamesave-browser.yaml | from pause menu |
| Debug panel | DEBUG_PANEL chrome/ingame-debug.yaml, DebugMenuLogic | dev cheats: checkboxes (INSTANT_BUILD, UNLIMITED_POWER, BUILD_ANYWHERE, SHOW_GEOMETRY ...) and buttons (GIVE_CASH, LEVEL_UP ...); common widgets only |
| In-game chat | CHAT_ROOT, common ingame-chat.yaml, ingame-infochat.yaml | multiplayer leftover |
| In ChromeLayout, no entry point from city menus | lobby*.yaml, multiplayer-*.yaml, connection.yaml, map-chooser.yaml, color-picker.yaml, missionbrowser.yaml, editor.yaml, playerprofile.yaml, ingame-info*.yaml (briefing, objectives, stats, script error, lobby options), ingame-perf.yaml, ingame-debuginfo.yaml, ingame-debug-hpf.yaml, tooltips.yaml, dropdowns.yaml | only need shared widget art (button, textfield, checkbox, slider, scrollpanel, dropdown) |

## 2. HUD regions (PLAYER_WIDGETS)

Logic: LoadIngameChatLogic, CityHudLogic (loads the 16 panels), CityTopBarLogic, CityToolbarLogic (+ .Items/.Layout/.Orders/.Signatures).
Right sidebar is 226 px wide, laid out vertically by CityToolbarLogic.Layout (adapts 720p..4K; status block drops to 2x2 on short windows).

| Region | Widget ids | Shows / does |
|---|---|---|
| Sidebar top block (`sidebar/background-top` 226x295) | SIDEBAR_BACKGROUND_TOP | frame for cash row, minimap, top buttons |
| Cash + balance | CASH (LabelWithTooltip, CITY_TOOLTIP: full funds + balance/month), BALANCE | compact funds (red if < 0), signed monthly balance (green/red). Font steps down Bold, Regular, Small, Tiny |
| Top buttons | TOP_BUTTONS: ORDER_BULLDOZE (order icon `repair` = bulldoze tile), ORDER_INFOVIEWS (`beacon` = infoviews tile), ORDER_BUDGET (`sell` = budget tile), OPTIONS_BUTTON (MenuButton, `options` tile 40x38) | bulldoze tool (B), info views panel (I), budget panel (F), game menu (Esc) |
| Minimap | RADAR_MINIMAP (Radar 202x202) + MINIMAP_OVERLAY corner art (`minimap-overlay`: top-left, top-right, bottom-left, bottom-right 7x7; top/left/right empty) | click moves camera |
| Category tabs | CITY_TABS (CITY_TAB_BUTTON 25x~31, 16 px icon from `city-icons-small`) | up/down arrow tabs on overflow; order in section 6 |
| Build palette | CITY_PALETTE (CityPalette: 3 icons per row, cell 58x48, cost text below icon, wheel/scrollbar) in CITY_PRODUCTION | tool and building icons; states normal, hover, active, locked (disabled), unaffordable |
| Status block | CITY_STATUS: ROW1..3 (`sidebar/background-plainrow`), MILESTONE_STAT (icon `milestone`: name, pop/next, XP bar), HAPPINESS_STAT (icon `happiness`, % + bar, factor hover), POWER_STAT (icon `electricity`), WATER_STAT (icon `water`), DEMAND_BARS | used/produced + bar (red when short); DemandBars R,C,I,O bipolar -100..100 with factor tooltip |
| Order rows | CITY_ORDERS: ORDER_ROW1, ORDER_ROW2 (`sidebar/background-plainrow`), 5 CITY_ORDER_BUTTON per row (34x35 tiles, variants normal / -disabled / -active) | panel buttons, see below |
| Sidebar cap | SIDEBAR_CAP (`sidebar/background-bottom` 226x28) | bottom frame |
| Command bar (bottom-left) | COMMAND_BAR_BACKGROUND (`commandbar/background` 454x43), CITY_COMMANDS: SPEED_PAUSE, SPEED_1, SPEED_2, SPEED_3 (`command-button` + hover/pressed/disabled/highlighted states; icons `pause`, `play`, `fast`, `faster`), DATE label | Space, 1, 2, 3; highlight = current speed |
| Alert strip (top centre) | CITY_ALERTS 760x30; CITY_ALERT_CHIP (button 176x28: TIER ColorBlock + "name x count") | worst city problems (tier >= Problem, not Good) + wildfire/accident chips; click = locate |
| Transients | TRANSIENTS_PANEL | floating event text, hidden while a big panel is open |
| Vehicle picker | VEHICLE_PICKER (invisible full-map overlay) | click a vehicle to inspect |
| Tooltips | TOOLTIP_CONTAINER; CITY_TOOLTIP (TITLE, BODY), CITY_FACTOR_TOOLTIP (TITLE + NAME/VALUE rows) | all tooltips |
| Key handler | CITY_KEYS | Esc closes panels/tools/selection, Tab flips one-way road, Shift+I cycles info views |

Order buttons (5 per row, in this order; hidden when the provider is missing, disabled until the progression unlock key in brackets):
stats [tool:statistics], chirper (blinks on new chirp), production, policies [tool:policies], progression, districts [tool:districts], tiles (activates a tool, not a panel), transit, achievements, advisor (toggles the card).

Hotkeys (mods/city/hotkeys.yaml): CityToolRoad R, CityToolZoning Z, CityToolBulldoze B, CityInfoViews I, CityInfoViewNext Shift+I, CityBudget F, CityPause Space, CitySpeed1/2/3 = 1/2/3, CityToolNetworks E, CityToolIndustry V, CityToolTransit T, CityStats G, CityChirper K, CityProduction O, CityPolicies P, CityProgression J, CityDistricts Y, CityTiles U, CityAchievements X, CityAdvisor L.

## 3. Panels / windows

All are `CityPanel` (9-slice `dialog` background, `Visible: false`, self-positioning by Placement, optional Avoid sibling), loaded by CityHudLogic in this draw order. Only one big panel is open at a time (CloseAllPanels on toggle). Close: CLOSE button (Image ICON `close`, 32 px in 36x32 buttons or 16 px in 28x24 buttons) or Esc; Building, Info views and Tiles panels have no CLOSE button.
Shared row templates: CITY_BUDGET_ROW (NAME, VALUE), CITY_INFO_ROW (NAME, VALUE), CITY_SLIDER_ROW (NAME button, CitySlider SLIDER, VALUE), CITY_INFOVIEW_ITEM (text button used for every tab/mode/scope/action button), CITY_TAB_BUTTON, CITY_ORDER_BUTTON, CITY_NODE_BUTTON.

| Panel (id, size, placement, logic) | Tabs / pages | Widgets | Key data shown |
|---|---|---|---|
| CITY_ADVISOR_PANEL 360x200 BottomLeft, CityAdvisorLogic | none; cards paged with PREVIOUS / NEXT | TITLE (gold), STEP "n / total", CLOSE, TEXT, HINT (green), CityBar PROGRESS, LOCATE, PREVIOUS, NEXT | 9 tutorial steps (road, zone, power, water, people, services, budget, infoviews, grow) as `tutorial-<id>-title/-text/-hint`; urgent advisor messages first (with locate) |
| CITY_BUDGET_PANEL 600x520 Center, CityBudgetLogic | Budget, Taxes, Services, Fees, Loan (buttons BUDGET_TAB_<ID>) | TITLE, CLOSE, TABS, BUDGET_TICKER; PAGE_BUDGET: INCOME_HEADER/EXPENSES_HEADER, ScrollPanel INCOME_LIST / EXPENSE_LIST (CITY_BUDGET_ROW), INCOME_TOTAL, EXPENSE_TOTAL, BALANCE, CityGraph TREND (12 months income vs expenses); PAGE_TAXES: TAX_ROWS (4 sliders), TAX_DETAIL_HEADER, ScrollPanel TAX_DETAIL (sliders per education level / per resource); PAGE_SERVICES: SERVICES_HEADER/HINT, SERVICE_ROWS (13 sliders 50..150%, "x% (eff. y%)"); PAGE_FEES: FEES_HEADER/HINT, FEE_ROWS (sliders 50..200%); PAGE_LOAN: LOAN_HEADER, LOAN_ROWS (borrow slider), LOAN_INFO, LOAN_PREVIEW, LOAN_NOTE | income/expense line items (taxes, fees, loan interest/repayment, service wages, imports/exports, upkeep per service, construction, milestone rewards, refunds, transit fares/upkeep, trade, industry, recycling, crime losses, overdraft interest); tax rates -10..30% for residential, commercial, industrial, office; service budget per ServiceKind; fees: power, water, garbage, health, education (parking label exists); loan principal/limit/interest |
| CITY_INFOVIEWS_PANEL 556x308 BottomCenter, CityInfoViewsLogic | group tabs: services, networks, environment, city (+ "Off" button) | TITLE, TABS, MODES (CITY_INFOVIEW_ITEM grid, text only, no icons), HeatLegend LEGEND + LEGEND_LOW / LEGEND_DESC / LEGEND_HIGH, SUMMARY (up to 3 label:value lines), TRAFFIC_MODES (Flow / Volume buttons), CityGraph FLOW_CHART (24 h flow) | 34 views (section 6), colour ramps Good, Bad, Pollution, Blue, Green, Category, Resource |
| CITY_STATS_PANEL 700x570 Center, CityStatsLogic | categories: overview, population, economy, city, utilities, environment, traffic | TITLE, CLOSE, CATEGORIES, SERIES (toggle list with colour swatches), SCALE (year / five / all), CityGraph GRAPH (hover read-out), OVERVIEW (bars: age groups, education levels, workers, unemployed, students, homeless, tourists), EMPTY | 33 time series (section 6) |
| CITY_CHIRPER_PANEL 340 x 5/12 window height, BottomRight, CityChirperLogic | none | TITLE, CLOSE, ScrollPanel FEED of CITY_CHIRP_TEMPLATE (AUTHOR with likes, TEXT, LOCATE button), EMPTY | milestones, population, shortages, weather, problems, citizen events (born, graduated, died, arrested), industry, flood, achievement/node/tile messages; locate on click |
| CITY_PRODUCTION_PANEL 680x520 Center, CityProductionLogic | none | TITLE, CLOSE, HEADER (H_NAME, H_PRODUCED, H_CONSUMED, H_IMPORTED, H_EXPORTED, H_STOCK, H_BALANCE), ScrollPanel LIST of CITY_PRODUCTION_ROW (NAME button, PRODUCED, CONSUMED, IMPORTED, EXPORTED, STOCK, CityBar BALANCE), CHART_TITLE, CityGraph CHART | 36 resources (section 6) |
| CITY_POLICIES_PANEL 620x500 Center, CityPoliciesLogic | scope: City / District (+ left/right arrows to pick district) | TITLE, CLOSE, SCOPE, DISTRICT_NAME, ScrollPanel LIST of CITY_POLICY_ROW (TOGGLE with CHECK icon `check`, NAME with tooltip, CitySlider SLIDER, VALUE, UPKEEP), DESCRIPTION | 18 policies (8 city, 10 district), locked state, upkeep |
| CITY_PROGRESS_PANEL 760x520 Center, CityProgressLogic | none (two lists) | TITLE, CLOSE, MILESTONE, CityBar XP_BAR, POINTS (dev points), PERMITS, MILESTONES_HEADER, ScrollPanel MILESTONES (rows name + XP), TREE_HEADER, ScrollPanel TREE (block per tree, CITY_NODE_BUTTON grid 3 per row, shows name + cost / owned), NODE_INFO | 20 milestones, 27 dev-tree nodes in 11 trees, unlock keys |
| CITY_DISTRICTS_PANEL 330x470 TopRight, CityDistrictsLogic | none | TITLE, CLOSE, ACTIONS (New, Paint, Erase, Delete buttons), ScrollPanel LIST of CITY_DISTRICT_ROW (SWATCH ColorBlock, NAME, POPULATION), TextField NAME_FIELD (rename), DETAILS (CITY_INFO_ROW: population, households, jobs, happiness, land value), HINT | painting uses UiAreaToolGenerator (drag rectangle, "N cells" label) |
| CITY_TRANSIT_PANEL 600x460 Center, CityTransitLogic | none | TITLE, CLOSE, ScrollPanel LIST of CITY_LINE_ROW (SWATCH, NAME, INFO, CityBar USAGE, PROFIT), EMPTY, SETTINGS (ticket price slider 0..500, vehicles slider, delete-line button) | lines of bus, tram, metro, train (taxi stands only) |
| CITY_ACHIEVEMENTS_PANEL 600x480 Center, CityAchievementsLogic | none | TITLE, CLOSE, SUMMARY (gold), ScrollPanel LIST of CITY_ACHIEVEMENT_ROW (ICON `achievements`, NAME, DESCRIPTION, CityBar PROGRESS, XP) | 28 achievements |
| CITY_TILES_PANEL 400x132 TopCenter, CityTilesLogic | none (shown while tile tool active) | TITLE, ROWS (CITY_INFO_ROW: tiles owned/total, permits, status, price, upkeep, land, resources), LEGEND (owned / buyable / locked swatches) | map tile purchase hover info; world overlay marks tiles (TileBorderOverlay) |
| CITY_BUILDING_PANEL 280x220 TopRight, CityBuildingInfoLogic (+ .Hub) | LIST_TABS: Residents / Workers | TITLE, SUBTITLE (zone + level or category), ROWS (CITY_INFO_ROW: status, level, households, jobs per education level, students, beds, company, power %, water %, sewage, road, happiness (factor hover), land value, rent, upkeep), ACTIONS (CITY_INFOVIEW_ITEM buttons), ScrollPanel LIST (ScrollItem LIST_TEMPLATE NAME) | selected building. ACTIONS: service upgrades (name + cost), district restriction (All + per district), hub area paint / clear, clearcut toggle, product choice (grain, vegetables, livestock, cotton) |
| CITY_CITIZEN_PANEL 280x262 TopRight, CityCitizenInfoLogic | none | TITLE (name), SUBTITLE (age line), CLOSE, ROWS (education, job, home, activity, happiness with factor hover, health, cash), HOME, FOLLOW, WORK buttons | selected citizen (click from building resident list) |
| CITY_VEHICLE_PANEL 260x232 TopLeft, CityVehicleLogic | none | TITLE, CLOSE, ROWS (purpose, mode, status), CityBar PROGRESS (trip), FROM, TO, DRIVER (buttons that locate), FOLLOW | selected vehicle; follow camera |
| Tooltips CITY_TOOLTIP, CITY_FACTOR_TOOLTIP | n/a | Background, TITLE, BODY / NAME, VALUE | section 2 |
| CITY_NEWGAME_PANEL | n/a | see section 1 | |

World-space UI (not widgets, drawn in the world): problem notification icons above buildings (`envicons`: tiles 8 frames per ProblemTier, glyphs 24 frames 16x16; `statusicons` 6 frames), info view heat overlay (`overlays/heat` 11 frames), zone overlay (`overlays/zone` 7 frames), grid, valid / invalid placement tints, tile border overlay, drag labels ("N cells", price per cell/stop; CityAnnotationText), road tool preview, selection boxes, night lights, snow, flood overlays.

## 4. Widget types

Custom (OpenRA.Mods.City/Widgets):

| Widget | Used in | What it draws / states |
|---|---|---|
| CityPanel (BackgroundWidget) | 15 panels | 9-slice `dialog`; Placement (Free, Center, TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight), Avoid, clamps itself to the area free of sidebar, command bar, alert strip |
| CityPalette | CITY_PALETTE | rows of 58x48 icon cells on `sidebar/background-iconrow`, cost text (Small), states normal / hover / active / disabled / unaffordable, scrollbar in right frame |
| CityStat | MILESTONE/HAPPINESS/POWER/WATER_STAT | 16 px icon, label left, value right, thin bar under; drops label when too narrow; value colour + bar colour by state |
| DemandBars | DEMAND_BARS | 4 vertical bipolar bars R, C, I, O, letters (Bold), hover factor tooltip |
| CityBar | advisor, achievements, XP, vehicle trip, transit usage, production balance | flat 0..100 bar, dark track + coloured fill |
| CitySlider (InputWidget) | budget, policies, transit rows | integer slider with drag value, step, FillColor, OnChange / OnCommit |
| CityGraph | budget TREND, stats GRAPH, production CHART, traffic FLOW_CHART | multi-series line chart, 4 grid lines + labels, hover read-out |
| HeatLegend | info views LEGEND | gradient strip for the active ramp |
| FactorHover | happiness, demand | invisible region opening CITY_FACTOR_TOOLTIP |
| VehiclePicker | VEHICLE_PICKER | click-through overlay selecting vehicles |
| CityTextBackdrop | TRANSIENT_LINE_TEMPLATE | translucent box behind a label |
| CityRows (helper) | budget, transit | builds slider rows (name, slider, value) |

Common widgets used by city yaml (counts of declarations): Label 125, Container 105, Button 59 (states normal, hover, pressed, disabled, highlighted + highlighted-hover/-pressed/-disabled), Image 43, Checkbox 24 (checkbox, -hover, -pressed, -disabled, -highlighted..., toggle variants, tick / cross / mute marks), ScrollPanel 15 (+ ScrollItem: nohover, hover, pressed, highlighted; scrollpanel-button up/down arrows, `scrollpanel-decorations`), Background 13, DropDownButton 7 (`dropdown-decorations`, `dropdown-separators`), TextField 3 (normal, hover, disabled, focused), ColorBlock 3, Slider 2 (`slider-track`, `slider-thumb` hover/pressed/disabled), MenuButton 2, LabelWithTooltip 2, ProgressBar (`progressbar-bg`, `progressbar-thumb`), Radar, MapPreview, LabelForInput, TextNotificationsDisplay, ClientTooltipRegion, LogicTicker, LogicKeyListener. Not used in city yaml (only in common dialogs): ProgressBar variants, ListBox style lists, Sprite buttons, lobby widgets.

Generated panel kinds (CityChromeGenerator.Panels.cs): Button, Compact, Dialog, Toolbar, Thumb, Raised, Track, Well, Tooltip/Frame, Item, Header, Bar, Stripe.
Image collections in chrome.yaml: sidebar, sidebar-button*, sidebar-bits, commandbar, minimap-overlay, power-icons, order-icons (legacy aliases repair, beacon, sell, options, power), command-button*, flags (city, Random, spectator), dialog, dialog2 (raised), dialog3 (well), dialog4 (frame), dialog5 (centre only), separator, scrollheader(-highlighted), button*, button-purchase*, textfield*, checkbox*, checkbox-toggle*, checkmark-tick / -cross / -mute (+ -highlighted), slider*, progressbar-*, scrollpanel-*, scrollitem*, scrollpanel-decorations, dropdown-decorations, dropdown-separators, lobby-bits, reload-icon, music, editor, logos, loadscreen-stripe. chrome-city.yaml: city-icons (32 px), city-icons-small (16 px), city-order-icons (34x35 + -disabled/-active), city-panel, city-toolbar, city-tooltip, city-button* (8 states). chrome-buildings.yaml: city-buildicons (48x48), city-buildicons-extra (48x48).

## 5. Icons (every name currently generated)

Source: mods/city/bits/chrome/cityicons.vec (vector icons, exported by tools/uiexport.py from tools/uicityicons.py, uicityicons2.py (road-bridge, achievements, advisor, ...), uiglyphs.py, uibaseglyphs.py). `city-icons` set = 112 icons (rendered at 32 px and 16 px). `glyphs` set = 64 small glyphs used by common dialogs / order tiles.

city set, grouped:
- Build category tabs (16): road, zoning, networks, power, water, police, fire, health, education, parks, garbage, deathcare, comms, admin, industry, transit. (Category `signature` has NO icon: tab falls back to `services`.)
- Tools: bulldoze, dezone, infoviews, budget, area-paint, area-clear (also reused for every "remove" tool), info, locate, follow, eye, lock, arrow-left, arrow-right, up, down, plus, minus, check, close.
- Roads (14): road, road-street, road-gravel, road-avenue, road-boulevard, road-highway, road-draw, road-oneway, road-replace, road-signal (reused by yield/stop/signal/default junction control), road-roundabout (reused by large roundabout), road-interchange (ramp), road-bridge. (road type `alley` has no icon, falls back to `road`; road add-ons reuse parks, police, power, bus-stop, bus, road-oneway.)
- Zones (12 + 2): zone-res-low, zone-res-high, zone-res-row, zone-res-med, zone-res-mixed, zone-res-lowrent, zone-com-low, zone-com-high, zone-ind, zone-off, zone-off-high, zone-warehouse, plus `zoning` (generic / fallback) and `dezone`.
- Utilities / networks: power (alias electricity), water, power-line, pipe, sewage, transformer, battery, networks.
- Services: police, fire, health, education, parks, garbage, deathcare, comms, post, admin, services, school.
- Industry hubs: industry, hub-farm, hub-forestry, hub-quarry, hub-mine, hub-oil.
- Transit: transit, bus, taxi, tram, metro, train, bus-stop, bus-depot, line.
- Info views / panels / order tiles: infoviews, budget, stats, chirper, production, policies, progression, districts, tiles, achievements, advisor, milestone.
- Time controls: pause, play, fast, faster (aliases speed-1, speed-2, speed-3).
- Stats / HUD readouts: money, population, happiness, demand, traffic, pollution, tax, xp, dev-point, permit, loan, fee, citizen, home, work, tourist, hotel.
- Status / misc: warning, info, lock, eye.

glyphs set (64): check, check-faint, cross, cross-faint, speaker, speaker-muted, arrow-up/down/left/right, play, pause, stop, next, prev, fastforward, reload, spinner-0..11, clock, bolt, bolt-critical, coin, admin-registered, admin-anonymous, player-registered, player-anonymous, bot, select, tiles, overlays, actors, tools, history, erase, copy, paste, undo, redo, spawn-claimed, spawn-unclaimed, spawn-disabled, colorpicker, flag-city, flag-random, flag-spectator, lock, lock-disabled, key, key-disabled, kick, admin-tiny, hue-marker, muted-indicator. Order tile glyph rule: glyph = city icon of the tile name, except `options` uses glyph `tools` and `power` uses `bolt`.

Order tiles (city-order-icons, 34x35, each with normal / -disabled / -active; body colour pair in uistyle.yaml OrderTiles): bulldoze, infoviews, budget, stats, chirper, production, policies, progression, districts, tiles, transit, achievements, advisor, options (40x38), power.

Build icons (48x48 sprite thumbnails rendered from building art, NOT hand-drawn): `city-buildicons` regions: busdepot, busstop, cemetery, cityhall, clinic, college, crematorium, farm-hub, fire-helipad, firehouse, firestation, firewatch, forestry-hub, highschool, hospital, hydro-dam, incinerator, landfill, medevac-helipad, mine-hub, oil-hub, park-large, park-small, plaza, police, police-hq, policebox, postoffice, powerplant-coal, powerplant-gas, powerplant-nuclear, prison, quarry-hub, recycling, school, sewage-outlet, solarplant, sortingcenter, sportsfield, telecom-mast, telecom-tower, transformer, treatment-plant, university, waterpump, waterpump-large, watertower, welfare, windturbine. `city-buildicons-extra`: battery, cargoairport, cargoharbor, fish-hub, metrostation, railyard, sig-bazaar, sig-fuel-plant, sig-garden-row, sig-glass-hub, sig-mall, sig-sky-tower, sig-villa, taxidepot, trainstation, tramdepot. Generated by tools/gen_svc.py and tools/uibuildicons.py. Fallback for an actor without icon: city icon `services`.

Sidebar / chrome images with `glyph` recipes: production-tooltip-time (clock), production-tooltip-power (bolt), production-tooltip-cost (coin), indicator-muted, power-normal (bolt), power-critical (bolt-critical), flags city / Random / spectator.

Problem icons (world): `envicons` tiles (8 frames: Minimal, Info, Problem, Warning, Major, Error, Fatal, Good) + glyphs (24 frames, 16x16, frame = CityProblem - 1; generated by tools/gen_env.py, not by the ui tools): nopower, nowater, nosewage, dirtywater, noroad, abandoned, unhappy, noworkers, nocustomers, nogoods, garbage, fire, crime, sick, ambulance, highrent, traffic, airpollution, groundpollution, noise, leveledup, collapsed, noservice, flooded. `statusicons` (6 frames, genworld.py).

Cursors (tools/uicursors.py, 32x32 pixel cursors, cursors.yaml): default, select, generic-blocked, move, move-blocked, sell, sell-blocked, city-road, city-zone, city-bulldoze, city-place (crosshair + badge road / zone / bulldoze / place), city-blocked, joystick-all, scroll-{t,tr,r,br,b,bl,l,tl}, scroll-*-blocked (8), joystick-{t..tl} (8), joystick-*-blocked (8). Total 48.

Logo: tools/uilogo.py, CityChromeGenerator.Logo.cs (`logos/logo` 256 px).

## 6. Data lists

### Build categories (sidebar tabs, CityToolbarLogic.TabOrder; tab shown only if it has items)
Tabs = 16 + unknown categories appended: road, zoning, networks, power, water, police, fire, health, education, parks, garbage, deathcare, comms, admin, industry, transit, then `signature` (7 items, appended, no icon, fluent label missing: shown with fallback).
Hotkey tabs: road R, zoning Z, networks E, industry V, transit T.

| Tab | Items (palette order) | Count |
|---|---|---|
| road | road types: street, gravel, avenue, boulevard, highway, alley; modes: draw, oneway, replace, paired, bridge; prefabs: roundabout, roundabout-large, ramp; junction control: control-yield, control-stop, control-signal, control-default; add-ons: trees, barrier, lights, parking, buslane, bikelane + addon-remove | 6 + 5 + 3 + 4 + 6 + 1 = 25 |
| zoning | zone:ResidentialLow, ResidentialHigh, CommercialLow, CommercialHigh, Industrial, Office, then (if growables exist) ResidentialRow, ResidentialMedium, ResidentialMixed, ResidentialLowRent, OfficeHigh, Warehouse, then dezone | 6 + 6 + 1 = 13 |
| networks | powerline, pipe-water, pipe-sewage, pipe-both, powerline-remove, pipe-remove (tools only; auto-activates power/water grid info view) | 6 |
| power | transformer, battery, windturbine, powerplant-coal, solarplant, powerplant-gas, hydro-dam, powerplant-nuclear | 8 |
| water | sewage-outlet, treatment-plant, watertower, waterpump, waterpump-large | 5 |
| police | policebox, police, police-hq, prison | 4 |
| fire | firehouse, firestation, fire-helipad, firewatch | 4 |
| health | clinic, hospital, medevac-helipad | 3 |
| education | school, highschool, college, university | 4 |
| parks | park-small, plaza, park-large, sportsfield | 4 |
| garbage | landfill, incinerator, recycling | 3 |
| deathcare | cemetery, crematorium | 2 |
| comms | postoffice, sortingcenter, telecom-mast, telecom-tower | 4 |
| admin | cityhall, welfare | 2 |
| industry | area-paint, area-clear (need a selected hub), farm-hub, forestry-hub, quarry-hub, mine-hub, oil-hub, fish-hub | 2 + 6 |
| transit | busstop, taxistand, tramstop, tramtrack, tramtrack-remove, rail, rail-remove, busline, tramline, metroline, trainline, stop-remove, then placeables busdepot, taxidepot, metrostation, tramdepot, trainstation, railyard, cargoharbor, cargoairport | 12 + 8 |
| signature | sig-villa, sig-garden-row, sig-sky-tower, sig-bazaar, sig-mall, sig-glass-hub, sig-fuel-plant | 7 |

Placeable total 64 (+ busstop icon). Locked items show grey with "unlocks at N population" or a progression lock text.

### Info views (34, grouped as in the panel; Id = fluent suffix label-infoview-<Id>)
- services (12): power, water, garbage, health, deathcare, education, police, crime, fire, parks, telecom, post
- networks (7): powergrid, watergrid, sewage, roads, traffic, transit, freight
- environment (7): pollution, air, ground, noise, groundwater, resources, landvalue
- city (8): happiness, level, attainment, wealth, age, production, tourism, districts
Ramps: Good (red to green), Bad (green to red), Pollution (clear, orange, brown), Blue (pale to deep blue), Green (dark to bright), Category (16 distinct colours), Resource (6 hues: fertile, forest, ore, oil, stone, fish). Plus `Off`.

### Services (ServiceKind, 13) and service-related tabs
Power, Water, Sewage, Garbage, Health, Deathcare, Education, Police, Fire, Parks, Telecom, Post, Admin (budget sliders in this order: Police, Fire, Health, Education, Garbage, Deathcare, Parks, Power, Water, Sewage, Telecom, Post, Admin). Fees: power, water, garbage, health, education.

### Zone types (ZoneType, 12 + None) and demand categories
ResidentialLow, ResidentialHigh, CommercialLow, CommercialHigh, Industrial, Office, ResidentialRow, ResidentialMedium, ResidentialMixed, ResidentialLowRent, OfficeHigh, Warehouse. Demand / tax categories (4, colours): Residential 7ED957, Commercial 5AB4F0, Industrial F2C94C, Office B07CE8.
Citizen enums used in UI: AgeGroup (Child, Teen, Adult, Senior), EducationLevel (Uneducated, Poorly, Educated, Well, Highly), CitizenActivity (Home, Working, Studying, Shopping, Leisure, Travelling, Hospital, Prison, Moving, Dead), TravelMode (Walk, Car, Transit, Taxi, Bike, Truck, ServiceVehicle, EmergencyVehicle, Bus), TransitMode (Bus, Taxi, Tram, Metro, Train), PropertyKind (Residential, Commercial, Industrial, Office, Warehouse, Extractor, Service, Transit, Signature).

### Road types (6) and add-ons (6)
street, gravel, avenue, boulevard, highway, alley. Add-ons: trees, barrier (sound), lights, parking, buslane, bikelane. Tools: oneway (reversible with Tab), replace, paired, bridge, roundabout (2 sizes), ramp, junction control (yield, stop, signal, default).

### Policies (18)
City scope (8): min-fare, import-services, smoking-ban, education-boost, free-transit, pollution-management, city-promotion, high-speed-highways. District scope (10): energy-saving, water-saving, recycling, speed-bumps, parking-fee, small-business, heavy-traffic-ban, gated-community, industrial-planning, combustion-ban. Some have sliders and upkeep.

### Problem / status types (CityProblem, 24) with base to escalated tier
nopower (Warning to Major), nowater (Warning to Major), nosewage (Warning to Major), dirtywater (Warning to Major), noroad (Problem), abandoned (Problem), unhappy (Info to Warning), noworkers (Info), nocustomers (Info), nogoods (Problem), garbage (Warning to Major), fire (Major to Fatal), crime (Info to Warning), sick (Problem), ambulance (Major), highrent (Info), traffic (Warning), airpollution (Warning), groundpollution (Warning), noise (Warning), leveledup (Good), collapsed (Error), noservice (Problem), flooded (Warning to Major).
ProblemTier (8): Minimal (grey), Info (6BB8F0), Problem (F2D04C), Warning (F28C30), Major (E54B3C), Error (A82020), Fatal (301010), Good (green 5CD67A).
Alert-only chips: wildfire (Major), accident (Warning). Chirp categories: milestone, population, power/water shortage, broke, snow, storm, heat, cold, problem-*, citizen born/graduated/died/arrested, flood warning/damage, lightning, trees dying, deposit low/depleted, freight blocked, service fire, prg milestone/achievement/node/tile.

### Milestones (20, Xp thresholds up to 195000)
Hamlet, Small Town, Town, Large Town, Small City, City, Big City, Metropolis, Large Metropolis, Great Metropolis, Grand Metropolis, Capital, Megacity I to VIII. Each gives money, dev points, map tiles, unlock keys (zone:*, tool:*, service:*, building ids, landmark:n).

### Development tree (27 nodes, 11 trees)
roads (5): road-roundabout, road-oneway, road-avenue, road-highway, road-boulevard. electricity (5): elec-gas, elec-battery, elec-solar, elec-hydro, elec-nuclear. water (2): water-treatment, water-pump-large. health (2): health-hospital, health-crematorium. education (3): edu-highschool, edu-college, edu-university. police (2): police-hq, police-prison. garbage (2): garbage-incinerator, garbage-recycling. parks (1): park-sports. comms (2): comms-tower, comms-sorting. admin (2): admin-welfare, admin-cityhall. transport (1): transit-metro. Costs 1/2/4/8, tiers 1..4.

### Achievements (28)
first-steps, small-town-spirit, big-city-life, metropolis, six-figures, all-smiles, in-the-black, balanced-books, millionaire, full-service, transit-city, mass-transit, eco-city, industrial-giant, business-hub, top-of-the-class, explorer, land-baron, calling-the-shots, wide-variety, city-planner, making-a-mark, tourist-trap, simply-irresistible, welcome-one-and-all, scholar, small-city, last-mile-marker.

### Stats series (33) by category
population (8): population, households, workers, unemployed, jobs, students, tourists, buildings. economy (3): funds, income, expenses. city (12): happiness, health, land-value, demand-residential, demand-commercial, demand-industrial, svc-crimes, svc-sick, svc-deaths, svc-fires, svc-garbage-generated-t, svc-garbage-collected-t. utilities (4): power-produced, power-used, water-produced, water-used. environment (4): pollution-ground, pollution-air, pollution-noise, temperature. traffic (2): traffic-flow, vehicles. Time scales: year, five years, all.

### Resources (production panel, 36)
Grain, Vegetables, Livestock, Cotton, Wood, Ore, Oil, Stone, Timber, Petrochemicals, Metals, Concrete, Food, Beverages, Textiles, Plastics, Furniture, Electronics, Vehicles, Machinery, Software, Financial, Media, Meals, Entertainment, Coal, Fish, Steel, Minerals, Chemicals, Pharmaceuticals, ConvenienceFood, Paper, Telecom, Lodging, Recreation. Natural resource kinds (map overlay): fertile, forest, ore, oil, stone, fish.

### Notifications / alerts
Alert strip chips (problems >= Problem tier + wildfire + accident), transient text lines, chirper feed, building problem icons, advisor card, audio notifications (mods/city/audio/notifications.yaml), tutorial steps (9). Colours: Good 5CD67A, Bad FF6B5E, Warn F2C94C, Muted B4C4D0, Accent E8A030, Gold F2C94C.

## 7. Missing for a CS2-style game (checked against code)

Already present (do not list as missing): loan page, per-category/per-education/per-resource taxes, service budgets, fees, district painting + per-district policies + district-restricted services, building upgrades (service upgrade buttons), map tile purchase, development tree + milestones, achievements, chirper with likes and locate, stats graphs, production/trade panel, transit line tools + transit panel (ticket price, vehicle count), vehicle and citizen inspection with follow, info views with legends, advisor/tutorial, factor tooltips, alert strip, hub area painting, signature buildings, save/load/settings (common dialogs).

Genuinely missing:
- Road / net segment inspector (selecting a road shows nothing: no lanes, speed, traffic, upkeep, upgrade-in-place; roads are not actors).
- Road tool modes: no straight / simple curve / complex curve / grid selector, no snapping toggles, no length / cost preview panel (only drag labels).
- Zoning tool options: rectangle drag only; no brush size, fill, or zone density selection overlay; no zone "colour legend" panel.
- Terraform / terrain tools and water tools (terrain is flat, no height UI), tree / prop placement tool, sandbox / unlimited-money toggles outside debug.
- Service coverage radius preview while placing buildings (no radius ring or coverage heat on placement).
- Transit line editor depth: no stop-by-stop line editor, no line colour picker or rename field, no schedule / ticket per mode panel, no passenger flow view per line, no transit overview map.
- Citizen life path / history, citizen and household search, building search, "find by name" box, no entity list.
- Notification centre / event log panel (only 5-line transient text, alert strip and chirper; no persistent filterable log), no per-building problem list panel, no notification settings.
- City radio / music UI (music player exists in common pause menu but Music is empty; no radio stations, no ad / news ticker).
- Photo mode (hide UI, free camera, filters), screenshot button, no camera bookmarks.
- Camera controls on screen (zoom / rotate / go-to-home buttons, compass); no zoom indicator or map overview toggle.
- Time / weather HUD: no clock / time-of-day, season, temperature or weather indicator (date only); no disaster alerts panel or disaster tool.
- City overview / "City Info" dashboard (population pyramid, employment, education breakdown exist only as the Stats overview tab); no happiness / health / crime / education detailed panel with factor lists beyond tooltips.
- Economy overview: no budget forecast, no per-district budget, no trade deals panel, no cashflow summary strip in HUD beyond balance; no "unemployment / jobs" gauges in HUD.
- Content / mod manager, Paradox-style account, workshop browser; save game thumbnails and metadata (save browser is the common dialog).
- Options for new city (difficulty, map size / seed, starting funds, natural disasters on/off, unlock-all / sandbox) - only map choice.
- Statistics: no per-service coverage stat series, no export, no custom graph overlays.
- Building info extras: no efficiency / production breakdown, no resource input-output lines, no "demolish" or "relocate" button in the panel (bulldoze tool only), no policy per building.
- Info view legends have no icons: all 34 views are text-only buttons; policies, stats categories and budget tabs also text-only.
- Accessibility: no UI-scale preview, no colour-blind palettes for heat ramps, no tooltips on disabled reason for every control.

