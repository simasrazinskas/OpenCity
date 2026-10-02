#!/usr/bin/env python3
"""
iso_ui_build - renders every OpenCity iso UI design asset into mods/city/design/iso/ui/ and writes the Figma
manifests. Deterministic; re-run after changing any iso_ui_* generator.

    python3 mods/city/tools/iso_ui_build.py            # everything
    python3 mods/city/tools/iso_ui_build.py icons      # only some parts: system icons panels screens

Outputs (1x PNGs, RGBA, alpha 0/255):
    system/<stem>.png                 design-system sheets (Figma scale 2; ds-scale at 1)
    icons/<group>/<name>-16.png, -24.png
    panels/<stem>.png                 windows at UI 1x (scale 2); panels-1.5x/<stem>.png (scale 1)
    screens/<stem>.png                full-screen mockups (scale 1)
    manifest.json                     {"page": "Interface", "sections": [...]} (all four sections)
    manifest-<n>-<slug>.json          one file per section in the exact BRIEF format
"""

import importlib
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from iso_ui_kit import Canvas, OUT
from iso_ui_chrome import Theme
import iso_ui_icons as I
import iso_ui_system as SYS
import iso_ui_panels_a as PA
import iso_ui_screens as SC

PANEL_MODULES = ["iso_ui_panels_finance", "iso_ui_panels_people", "iso_ui_panels_transit", "iso_ui_menus"]
ALL_PANELS = list(PA.PANELS)
for m in PANEL_MODULES:
    try:
        mod = importlib.import_module(m)
    except Exception as e:  # a panel module still in progress must not block the rest
        print("skip", m, repr(e))
        continue
    if getattr(mod, "PANELS", PA.PANELS) is not PA.PANELS:
        ALL_PANELS += mod.PANELS
ALL_PANELS += [p for p in PA.PANELS if p not in ALL_PANELS]

PAGE = "Interface"
MENU_MODULE = "iso_ui_menus"
# panels also rendered at UI 1.5x
AT_15 = {"build-menu-health", "inspector-overview", "budget-services", "statistics-population", "citizen-overview",
         "transit-line-route", "settings-display", "milestones-devtree"}


def trim(c, m=2):
    a = c.a[:, :, 3] > 0
    ys, xs = np.nonzero(a)
    if len(xs) == 0:
        return c
    x0, x1, y0, y1 = max(0, xs.min() - m), min(c.w, xs.max() + 1 + m), max(0, ys.min() - m), min(c.h, ys.max() + 1 + m)
    return c.crop(x0, y0, x1 - x0, y1 - y0)


def render_panel(fn, wh, hh, kw, u):
    T = Theme(u)
    c = Canvas(T.s(wh) + T.s(200), T.s(hh) + T.s(200))
    fn(c, T, T.s(6), T.s(6), **kw)
    return trim(c)


def build(parts):
    sections = []
    if "system" in parts:
        groups = []
        for stem, title, fn in SYS.SHEETS:
            fn().save("system/%s.png" % stem)
            groups.append({"title": title, "columns": 1, "scale": 1 if stem == "ds-scale" else 2,
                           "items": [{"file": "system/%s.png" % stem, "label": stem}]})
        sections.append(("Design system", groups))
    if "icons" in parts:
        groups = []
        for g, title in I.GROUPS:
            names = [n for n in I.names(g) if n != "cat_housing"]
            if not names:
                continue
            for n in names:
                for sz in (16, 24):
                    I.ic(n, sz).save("icons/%s/%s-%d.png" % (g, n, sz))
            for sz in (16, 24):
                groups.append({"title": "%s (%d px)" % (title, sz), "columns": 12 if sz == 16 else 10, "scale": 2,
                               "items": [{"file": "icons/%s/%s-%d.png" % (g, n, sz), "label": I.label(n)} for n in names]})
        sections.append(("Icons", groups))
    if "panels" in parts or "screens" in parts:
        hud, menus = [], []
        by_title = {}
        if "panels" in parts:
            for stem, title, fn, w, h, kw in ALL_PANELS:
                is_menu = fn.__module__ == MENU_MODULE
                render_panel(fn, w, h, kw, 1).save("panels/%s.png" % stem)
                item = {"file": "panels/%s.png" % stem, "label": title}
                key = (is_menu, fn.__module__)
                by_title.setdefault(key, []).append(item)
                if stem in AT_15:
                    render_panel(fn, w, h, kw, 1.5).save("panels-1.5x/%s.png" % stem)
                    by_title.setdefault((is_menu, "1.5x"), []).append({"file": "panels-1.5x/%s.png" % stem, "label": title + " (1.5x)"})
        titles = {"iso_ui_panels_a": "Build menu and building inspector (RCT2 ride-window tabs)",
                  "iso_ui_panels_finance": "Budget, statistics, production, city info",
                  "iso_ui_panels_people": "Citizens, vehicles, chirper, advisor, progression, policies, districts",
                  "iso_ui_panels_transit": "Transit lines, info views + legends, alerts, map tiles, road inspector, tool palettes",
                  "iso_ui_menus": "Menu and dialog windows", "1.5x": "Same windows redrawn at UI 1.5x"}
        if "screens" in parts:
            scr_hud, scr_menu = [], []
            for stem, title, fn, w, h, u in SC.SCREENS:
                fn().save("screens/%s.png" % stem)
                item = {"file": "screens/%s.png" % stem, "label": title}
                (scr_menu if fn.__module__ == MENU_MODULE else scr_hud).append(item)
            if scr_hud:
                hud.append({"title": "Main HUD mockups (placeholder world; final scene comes from KIT)", "columns": 2, "scale": 1, "items": scr_hud})
            if scr_menu:
                menus.append({"title": "Menu and dialog screens", "columns": 2, "scale": 1, "items": scr_menu})
        for (is_menu, mod), items in by_title.items():
            grp = {"title": titles.get(mod, mod), "columns": 4, "scale": 1 if mod == "1.5x" else 2, "items": items}
            (menus if is_menu else hud).append(grp)
        sections.append(("HUD & panels", hud))
        sections.append(("Menus & dialogs", menus))
    return sections


def main():
    parts = set(sys.argv[1:]) or {"system", "icons", "panels", "screens"}
    sections = build(parts)
    if parts == {"system", "icons", "panels", "screens"}:
        allm = {"page": PAGE, "sections": [{"section": s, "groups": g} for s, g in sections]}
        with open(os.path.join(OUT, "manifest.json"), "w") as f:
            json.dump(allm, f, indent=1)
        for i, (s, g) in enumerate(sections):
            slug = s.lower().replace(" & ", "-").replace(" ", "-")
            with open(os.path.join(OUT, "manifest-%d-%s.json" % (i + 1, slug)), "w") as f:
                json.dump({"page": PAGE, "section": s, "groups": g}, f, indent=1)
    n = sum(len(gr["items"]) for _, g in sections for gr in g)
    print("sections", [s for s, _ in sections], "items", n, "missing icon modules", I.MISSING)


if __name__ == "__main__":
    main()
