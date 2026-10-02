"""Loads every iso_ui_icons_* module and exposes ic(name, size) with a visible fallback for missing names."""

import importlib

import iso_ui_icon_dsl as D
from iso_ui_kit import Canvas, R

MODULES = ["iso_ui_icons_core", "iso_ui_icons_build", "iso_ui_icons_roads", "iso_ui_icons_transit",
           "iso_ui_icons_resources", "iso_ui_icons_info", "iso_ui_icons_status", "iso_ui_icons_hud",
           "iso_ui_icons_panels"]

# Figma/manifest order and titles of icon groups
GROUPS = [
    ("build", "Build categories (toolbar tabs)"), ("tools", "Tools"), ("networks", "Utility networks"),
    ("roads", "Roads, road tools, junctions, add-ons"), ("zones", "Zones"),
    ("transit", "Transit vehicles, stops, lines"), ("industry", "Industry hubs"),
    ("resources", "Resources and natural deposits"), ("infoview", "Info views"),
    ("status", "Status / problems (shared with world-space icons)"), ("time", "Time controls"),
    ("stats", "Stats and HUD readouts"), ("weather", "Weather"), ("seasons", "Seasons"),
    ("panels", "Panels / toolbar buttons"), ("ui", "Generic UI glyphs"),
]

MISSING = []

for m in MODULES:
    try:
        importlib.import_module(m)
    except ImportError as e:
        MISSING.append((m, str(e)))


def ic(name, n=16):
    if name in D.REG:
        return D.get(name, n)
    c = Canvas(n, n)
    c.fill(1, 1, n - 2, n - 2, R("red", 5))
    c.rect(0, 0, n, n, R("red", 0))
    return c


def names(group):
    return [n for n in D.ORDER if D.REG[n][0] == group]


def label(name):
    return D.REG[name][1] if name in D.REG else name
