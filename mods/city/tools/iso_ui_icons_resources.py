"""Resource / goods icons (group "resources") for the RCT2-style OpenCity icon set, plus the six natural
resource map-kind tiles. See iso_ui_icon_dsl for the DSL. Goods are flat front / 3-4 view mini objects."""

import math

import numpy as np

from iso_ui_icon_dsl import icon
from iso_ui_icons_core import grass_tile, drop

G = "resources"


# ---- local helpers ------------------------------------------------------------------------------------
def ball(I, cx, cy, r, ramp, s=4, ry=None):
    """Shaded round blob: dark rim, mid body, highlight to the upper left."""
    ry = ry or r
    I.ellipse(cx, cy, r, ry, ramp, s - 1)
    I.ellipse(cx - 0.3, cy - 0.3, r - 0.6, ry - 0.6, ramp, s)
    I.ellipse(cx - r * 0.3, cy - ry * 0.3, r * 0.55, ry * 0.55, ramp, s + 1)
    I.ellipse(cx - r * 0.4, cy - ry * 0.4, r * 0.22, ry * 0.22, ramp, 7)


def fish(I, cx, cy, sc=1.0):
    """Orange fish facing left."""
    I.poly([(cx + 3 * sc, cy), (cx + 7 * sc, cy - 3.2 * sc), (cx + 7 * sc, cy + 3.2 * sc)], "red", 4)
    I.poly([(cx - 1 * sc, cy - 2.8 * sc), (cx + 2.5 * sc, cy - 5 * sc), (cx + 3 * sc, cy - 2 * sc)], "red", 4)
    I.ellipse(cx, cy, 5.2 * sc, 3.3 * sc, "orange", 4)
    I.ellipse(cx - 0.3 * sc, cy - 0.7 * sc, 4.6 * sc, 2.2 * sc, "orange", 5)
    I.ellipse(cx - 0.6 * sc, cy + 1.7 * sc, 3.6 * sc, 1.1 * sc, "yellow", 6)
    I.line(cx - 2 * sc, cy - 2 * sc, cx - 2 * sc, cy + 2 * sc, "red", 3, 0.6)
    I.ellipse(cx - 3.2 * sc, cy - 0.9 * sc, 1.1 * sc, 1.1 * sc, "grey", 7)
    I.pixel(cx - 3.6 * sc, cy - 1.2 * sc, "grey", 0)


def arc(I, cx, cy, r, t, c, s, a0, a1):
    """Arc between angles a0..a1 (degrees, 0 = right, 90 = up)."""
    dx, dy = I.X - cx, I.Y - cy
    d = np.sqrt(dx * dx + dy * dy)
    ang = np.degrees(np.arctan2(-dy, dx))
    m = (d <= r) & (d >= r - t) & (ang >= a0) & (ang <= a1)
    I._paint(m, c, s)


def gear(I, cx, cy, r, ramp="grey", teeth=8):
    for i in range(teeth):
        a = 2 * math.pi * i / teeth
        ca, sa = math.cos(a), math.sin(a)
        pa, pb = -sa, ca
        r0, r1, w0, w1 = r - 1.5, r + 1.2, 1.5, 1.0
        I.poly([(cx + ca * r0 + pa * w0, cy + sa * r0 + pb * w0), (cx + ca * r1 + pa * w1, cy + sa * r1 + pb * w1),
                (cx + ca * r1 - pa * w1, cy + sa * r1 - pb * w1), (cx + ca * r0 - pa * w0, cy + sa * r0 - pb * w0)],
               ramp, 4)
    ball(I, cx, cy, r, ramp, 5)
    I.ellipse(cx, cy, r * 0.42, r * 0.42, None)
    I.ring(cx, cy, r * 0.42 + 0.9, 0.9, ramp, 2)


# ---- raw materials ------------------------------------------------------------------------------------
@icon("res_grain", G, "Grain")
def _(I):
    for x in (3.5, 6, 8, 10, 12.5):
        I.line(8, 14, x, 7, "sand", 5, 0.9)
    for x, y in ((3.2, 4.8), (5.8, 3.6), (8, 2.6), (10.2, 3.6), (12.8, 4.8)):
        I.ellipse(x, y, 1.5, 3, "yellow", 4)
        I.ellipse(x - 0.4, y - 0.4, 0.9, 2.3, "yellow", 6)
        I.pixel(x - 0.5, y + 1, "yellow", 2)
    I.rect(5, 9.5, 6, 2, "red", 5)
    I.rect(5, 9.5, 6, 0.8, "red", 6)
    I.rect(5.5, 11.5, 5, 3, "sand", 5)
    I.rect(5.5, 11.5, 1.5, 3, "sand", 6)


@icon("res_vegetables", G, "Vegetables")
def _(I):
    I.poly([(1.5, 14), (3.8, 7), (8.5, 10.5)], "orange", 5)
    I.poly([(1.5, 14), (3.8, 7), (5, 8.2)], "orange", 6)
    I.line(3.5, 7.2, 2, 3.2, "green", 5, 1.4)
    I.line(4.5, 7.5, 5.5, 3, "green", 6, 1.4)
    ball(I, 10.3, 9.3, 4.7, "green", 5)
    I.line(8, 7.5, 12.5, 10.5, "green", 3, 0.6)
    I.line(9.5, 12, 12.5, 7, "green", 7, 0.6)


@icon("res_livestock", G, "Livestock")
def _(I):
    I.poly([(3.5, 4.5), (5.5, 5), (5, 7)], "yellow", 6)
    I.poly([(12.5, 4.5), (10.5, 5), (11, 7)], "yellow", 6)
    I.ellipse(3, 7, 2, 1.4, "brown", 4)
    I.ellipse(13, 7, 2, 1.4, "brown", 3)
    I.ellipse(8, 8, 4, 5, "#f4eee4")
    I.ellipse(10, 5.8, 2, 2.3, "grey", 2)
    I.ellipse(8, 12, 3.4, 2.4, "#f0a0a0")
    I.pixel(6.6, 11.6, "red", 2)
    I.pixel(8.8, 11.6, "red", 2)
    I.rect(5.7, 7, 1.2, 1.3, "grey", 0)
    I.rect(9.3, 7, 1.2, 1.3, "grey", 0)
    I.rect(6, 6.8, 1, 0.5, "grey", 7)


@icon("res_cotton", G, "Cotton")
def _(I):
    I.poly([(8, 14.5), (7.4, 10), (8.6, 10)], "green", 4)
    I.poly([(8, 12.5), (2.5, 12), (5, 14), (8, 13.5)], "green", 5)
    I.poly([(8, 9), (2.8, 6.5), (3.5, 10.5), (6.5, 11.5)], "brown", 4)
    I.poly([(8, 9), (13.2, 6.5), (12.5, 10.5), (9.5, 11.5)], "brown", 3)
    I.poly([(5.5, 9.5), (10.5, 9.5), (9.5, 12), (6.5, 12)], "brown", 5)
    for x, y in ((5, 6.5), (11, 6.5), (8, 3.8), (6, 4.5), (10, 4.5)):
        ball(I, x, y, 2.9, "grey", 6)
    ball(I, 8, 6.8, 3.4, "grey", 6)


@icon("res_wood", G, "Wood")
def _(I):
    I.rect(2.5, 5, 9.5, 8.5, "brown", 4)
    I.rect(2.5, 5, 9.5, 2, "brown", 6)
    I.rect(2.5, 11.5, 9.5, 2, "brown", 3)
    I.line(3, 8.5, 11, 8.5, "brown", 3, 0.6)
    I.line(4, 10.2, 11, 10.2, "brown", 2, 0.6)
    I.ellipse(12, 9.25, 2.6, 4.25, "sand", 6)
    I.ring(12, 9.25, 2.2, 0.8, "orange", 4)
    I.ring(12, 9.25, 1.1, 0.6, "orange", 4)
    I.ellipse(2.5, 9.25, 1.2, 4.25, "brown", 5)


@icon("res_ore", G, "Ore")
def _(I):
    I.poly([(1.5, 13.5), (1.5, 9), (5, 4), (10, 3), (14.5, 8), (14.5, 13.5)], "grey", 4)
    I.poly([(1.5, 9), (5, 4), (10, 3), (8, 8), (4, 9.5)], "grey", 6)
    I.poly([(14.5, 8), (14.5, 13.5), (9, 13.5), (10, 8)], "grey", 3)
    for x, y, c, s in ((4, 11, "orange", 5), (7, 6, "yellow", 6), (11, 10.5, "yellow", 5), (9, 12, "orange", 6),
                       (11.5, 6.2, "orange", 5), (5.5, 7.5, "orange", 6)):
        I.rect(x, y, 1.8, 1.8, c, s)
    I.pixel(7, 6, "grey", 7)


@icon("res_oil", G, "Oil")
def _(I):
    I.rect(3.5, 3.5, 9, 10, "grey", 2)
    I.ellipse(8, 13.5, 4.5, 1.5, "grey", 2)
    I.rect(3.5, 3.5, 3, 10, "grey", 3)
    I.rect(4.2, 4.5, 1, 8.5, "grey", 5)
    I.rect(3.5, 5.5, 9, 1.2, "grey", 4)
    I.rect(3.5, 10.5, 9, 1.2, "grey", 4)
    I.ellipse(8, 3.5, 4.5, 1.6, "grey", 5)
    I.ellipse(8, 3.5, 3, 0.9, "grey", 1)
    drop(I, 8.5, 9.5, 1.5, "yellow", 5)


@icon("res_stone", G, "Stone")
def _(I):
    I.isobox(9, 14.5, 7, 5, 4, "grey", 6, 5, 3)
    I.isoquad(9, 14.5, [(-3.5, 0, 0), (-3.5, 0, 4), (-3.3, 0, 4), (-3.3, 0, 0)], "grey", 3)
    I.isobox(8.5, 8.5, 4, 3, 3.5, "grey", 7, 6, 4)


@icon("res_timber", G, "Timber")
def _(I):
    I.isobox(8, 14.5, 8, 4, 8, "orange", 7, 6, 4)
    for z in (2.7, 5.4):
        I.isoquad(8, 14.5, [(-8, 0, z), (0, 0, z), (0, 0, z + 0.5), (-8, 0, z + 0.5)], "orange", 2)
        I.isoquad(8, 14.5, [(0, 0, z), (0, -4, z), (0, -4, z + 0.5), (0, 0, z + 0.5)], "orange", 1)
    I.isoquad(8, 14.5, [(-5.5, 0, 0), (-4.5, 0, 0), (-4.5, 0, 8), (-5.5, 0, 8)], "red", 4)
    I.isoquad(8, 14.5, [(-1.5, 0, 0), (-0.5, 0, 0), (-0.5, 0, 8), (-1.5, 0, 8)], "red", 4)


@icon("res_petrochemicals", G, "Petrochemicals")
def _(I):
    I.rect(2.5, 5, 10.5, 9, "red", 4)
    I.rect(2.5, 5, 10.5, 1.5, "red", 6)
    I.rect(11, 5, 2, 9, "red", 3)
    I.rect(4, 2.5, 5, 2.5, "red", 3)
    I.rect(5.3, 3.5, 2.4, 1.2, None)
    I.rect(10, 2.5, 2, 2.5, "grey", 2)
    I.line(4, 7, 11, 12.5, "red", 3, 0.7)
    I.line(4, 12.5, 11, 7, "red", 3, 0.7)
    I.rect(5.5, 8.5, 4, 2, "yellow", 5)


@icon("res_metals", G, "Metals")
def _(I):
    def ingot(x, y, ramp, w=7.5, h=4):
        I.poly([(x, y + h), (x + w, y + h), (x + w - 1.2, y + 1.2), (x + 1.2, y + 1.2)], ramp, 4)
        I.poly([(x + 0.4, y + h - 0.4), (x + w - 2, y + h - 0.4), (x + w - 2.6, y + 1.8), (x + 1.6, y + 1.8)], ramp, 5)
        I.poly([(x + 1.2, y + 1.2), (x + w - 1.2, y + 1.2), (x + w - 2.2, y), (x + 2.2, y)], ramp, 7)
        I.rect(x + 1.5, y + 2.4, w - 5, 0.7, ramp, 7)
    ingot(1, 10, "grey")
    ingot(7.5, 10, "grey")
    ingot(4.2, 5.8, "yellow")


@icon("res_concrete", G, "Concrete")
def _(I):
    I.poly([(3, 6), (13, 6), (13.5, 13), (12, 14.5), (4, 14.5), (2.5, 13)], "grey", 5)
    I.poly([(10, 6), (13, 6), (13.5, 13), (12, 14.5), (10, 14.5)], "grey", 4)
    I.poly([(3, 6), (4, 2.8), (12, 2.8), (13, 6)], "grey", 6)
    I.rect(4, 2.8, 8, 0.8, "grey", 7)
    I.rect(4.5, 7.8, 6, 4.5, "orange", 5)
    I.rect(4.5, 7.8, 6, 1, "orange", 6)
    I.rect(5.5, 9.8, 4, 1, "grey", 2)


# ---- processed goods ----------------------------------------------------------------------------------
@icon("res_food", G, "Food")
def _(I):
    I.rect(3, 4.5, 10, 9, "grey", 5)
    I.rect(3, 4.5, 3, 9, "grey", 6)
    I.rect(10.5, 4.5, 2.5, 9, "grey", 3)
    I.ellipse(8, 13.5, 5, 1.7, "grey", 4)
    I.rect(3, 6.5, 10, 5, "red", 5)
    I.rect(3, 6.5, 3, 5, "red", 6)
    I.rect(10.5, 6.5, 2.5, 5, "red", 3)
    I.ellipse(8, 9, 2.3, 1.7, "yellow", 6)
    I.ellipse(8, 4.5, 5, 1.8, "grey", 6)
    I.ellipse(8, 4.5, 3.6, 1.1, "grey", 4)


@icon("res_beverages", G, "Beverages")
def _(I):
    I.rect(6.8, 2, 2.4, 5, "green", 5)
    I.poly([(6.8, 6), (9.2, 6), (11, 8.5), (5, 8.5)], "green", 5)
    I.rect(5, 8, 6, 6.5, "green", 5)
    I.rect(5, 8, 1.8, 6.5, "green", 6)
    I.rect(9.5, 6.5, 1.5, 8, "green", 3)
    I.rect(6.5, 1.5, 3, 1.6, "yellow", 5)
    I.rect(5, 10, 6, 2.8, "sand", 7)
    I.rect(9.5, 10, 1.5, 2.8, "sand", 5)
    I.rect(6.2, 11, 3, 0.8, "red", 4)


@icon("res_textiles", G, "Textiles")
def _(I):
    I.rect(4, 4.5, 9, 9, "purple", 5)
    I.rect(4, 4.5, 9, 2, "purple", 6)
    I.rect(4, 11.5, 9, 2, "purple", 3)
    I.rect(9.5, 4.5, 1, 9, "yellow", 5)
    I.ellipse(13, 9, 2, 4.5, "purple", 3)
    I.ellipse(4, 9, 2.2, 4.5, "purple", 6)
    I.ring(4, 9, 3.2, 0.7, "purple", 4)
    I.ring(4, 9, 1.6, 0.7, "purple", 4)
    I.poly([(5, 13.5), (11, 13.5), (14.5, 14.8), (6, 14.8)], "purple", 5)


@icon("res_plastics", G, "Plastics")
def _(I):
    I.ellipse(8, 13, 7, 2, "grey", 2)
    pts = [(4, 12, "blue"), (8, 12, "red"), (12, 12, "yellow"), (6, 9, "yellow"), (10, 9, "blue"),
           (8, 6, "red")]
    for x, y, c in pts:
        ball(I, x, y, 2.5, c, 5)
    I.pixel(7, 4.5, "red", 7)


@icon("res_furniture", G, "Furniture")
def _(I):
    I.rect(3, 1.5, 2.2, 12.5, "brown", 4)
    I.rect(3, 1.5, 0.8, 12.5, "brown", 6)
    I.rect(5.2, 3, 6.5, 1.3, "brown", 5)
    I.rect(5.2, 5.2, 6.5, 1.3, "brown", 5)
    I.rect(3, 8.5, 10.5, 2.2, "brown", 5)
    I.rect(4.5, 6.9, 9, 2, "red", 5)
    I.rect(4.5, 6.9, 9, 0.8, "red", 6)
    I.rect(11.3, 10.5, 2.2, 3.5, "brown", 3)
    I.rect(3, 10.5, 2.2, 3.5, "brown", 4)


@icon("res_electronics", G, "Electronics")
def _(I):
    I.rect(1.5, 1.5, 13, 13, "green", 4)
    I.rect(1.5, 1.5, 13, 1, "green", 6)
    I.rect(4.5, 4.5, 7, 7, "grey", 2)
    I.rect(5.5, 5.5, 5, 5, "grey", 3)
    I.rect(5.5, 5.5, 5, 1, "grey", 5)
    for p in (5, 7, 9, 11):
        I.rect(p - 0.5, 2, 1, 2.5, "yellow", 6)
        I.rect(p - 0.5, 11.5, 1, 2.5, "yellow", 5)
        I.rect(2, p - 0.5, 2.5, 1, "yellow", 6)
        I.rect(11.5, p - 0.5, 2.5, 1, "yellow", 5)
    I.pixel(6, 6, "yellow", 7)


@icon("res_vehicles", G, "Vehicles")
def _(I):
    I.rect(1.5, 8, 13, 4, "red", 4)
    I.rect(1.5, 8, 13, 1.2, "red", 6)
    I.poly([(3.5, 8), (6, 4), (10.5, 4), (12.5, 8)], "red", 5)
    I.poly([(5, 7.5), (6.6, 4.8), (8.2, 4.8), (8.2, 7.5)], "blue", 6)
    I.poly([(9, 7.5), (9, 4.8), (10.2, 4.8), (11.6, 7.5)], "blue", 5)
    I.rect(13.3, 9, 1.2, 1.4, "yellow", 6)
    I.rect(1.5, 9.5, 1, 1.2, "red", 2)
    for x in (4.7, 11.3):
        I.ellipse(x, 12.3, 2.3, 2.3, "grey", 1)
        I.ellipse(x, 12.3, 1, 1, "grey", 6)


@icon("res_machinery", G, "Machinery")
def _(I):
    gear(I, 8, 8, 5.3, "grey", 8)
    I.ring(8, 8, 2.3, 0.9, "orange", 5)


@icon("res_software", G, "Software")
def _(I):
    I.poly([(2, 2), (12, 2), (14, 4), (14, 14), (2, 14)], "blue", 4)
    I.rect(2, 2, 1, 12, "blue", 5)
    I.rect(5, 2, 6, 4, "grey", 6)
    I.rect(8.5, 2.8, 1.8, 2.6, "grey", 2)
    I.rect(4, 8, 8, 6, "#ffffff")
    I.rect(5, 9.5, 6, 0.8, "red", 5)
    I.rect(5, 11.5, 4, 0.8, "blue", 5)


@icon("res_financial", G, "Financial")
def _(I):
    I.poly([(1.5, 6), (8, 2), (14.5, 6)], "yellow", 5)
    I.poly([(8, 2), (14.5, 6), (8, 6)], "yellow", 4)
    I.rect(2, 6, 12, 1.5, "sand", 6)
    for x in (3, 7, 11):
        I.rect(x, 7.5, 2, 4.5, "sand", 7)
        I.rect(x + 1.2, 7.5, 0.8, 4.5, "sand", 5)
    I.rect(2, 12, 12, 1.5, "sand", 5)
    I.rect(1.5, 13.5, 13, 1.5, "sand", 4)
    I.ellipse(8, 4.8, 0.9, 0.9, "yellow", 7)


@icon("res_media", G, "Media")
def _(I):
    I.line(8, 5, 4.5, 1.8, "grey", 6, 0.8)
    I.line(8, 5, 11.5, 1.8, "grey", 6, 0.8)
    I.rect(2, 5, 12, 9, "brown", 4)
    I.rect(2, 5, 12, 1, "brown", 6)
    I.rect(3.5, 6.5, 7, 6, "blue", 3)
    I.rect(4, 7, 6, 5, "blue", 5)
    I.poly([(4, 7), (7, 7), (4, 10)], "blue", 7)
    I.rect(11.5, 7, 1.5, 1.5, "yellow", 5)
    I.rect(11.5, 9.5, 1.5, 1.5, "red", 5)
    I.rect(3, 14, 2, 1, "brown", 2)
    I.rect(11, 14, 2, 1, "brown", 2)


@icon("res_meals", G, "Meals")
def _(I):
    I.rect(2.2, 1.5, 0.9, 5, "grey", 6)
    I.rect(3.9, 1.5, 0.9, 5, "grey", 6)
    I.rect(1.5, 1.5, 0.9, 5, "grey", 6)
    I.rect(1.5, 5, 3.3, 1.5, "grey", 5)
    I.rect(2.4, 6.5, 1.5, 8, "grey", 5)
    I.ellipse(9.8, 9, 5.5, 4.7, "grey", 5)
    I.ellipse(9.8, 9, 5.5, 4.7, "#f4f4f0")
    I.ellipse(10.3, 9.5, 4.8, 3.9, "grey", 5)
    I.ellipse(9.8, 9, 3.6, 2.8, "#ffffff")
    I.ellipse(9, 8.5, 2.2, 1.5, "brown", 4)
    I.ellipse(8.5, 8, 1.2, 0.8, "brown", 6)
    I.ellipse(12, 9.3, 1.2, 0.9, "green", 5)
    I.ellipse(11, 10.3, 1, 0.7, "red", 5)


@icon("res_entertainment", G, "Entertainment")
def _(I):
    # sad (blue) mask behind, happy (yellow) in front
    I.ellipse(10.5, 7.5, 4, 5.3, "blue", 4)
    I.ellipse(10, 7, 3.3, 4.5, "blue", 5)
    I.rect(8.5, 5.5, 1.5, 1.2, "grey", 0)
    I.rect(11.2, 5.5, 1.5, 1.2, "grey", 0)
    I.poly([(8.5, 11), (12.5, 11), (11.5, 9.5), (9.5, 9.5)], "grey", 0)
    I.ellipse(6, 9, 4.2, 5.3, "yellow", 4)
    I.ellipse(5.6, 8.6, 3.5, 4.5, "yellow", 6)
    I.rect(3.7, 7, 1.6, 1.2, "grey", 0)
    I.rect(6.6, 7, 1.6, 1.2, "grey", 0)
    I.poly([(3.5, 10), (8.5, 10), (7.5, 12.2), (4.5, 12.2)], "grey", 0)


@icon("res_coal", G, "Coal")
def _(I):
    def lump(pts, hi):
        I.poly(pts, "grey", 2)
        I.poly(hi, "grey", 4)
    lump([(1.5, 13.5), (1.5, 9.5), (4.5, 7), (8, 8), (8.5, 13.5)], [(1.5, 9.5), (4.5, 7), (6, 7.5), (4, 10)])
    lump([(7, 13.5), (7, 8), (10, 3.5), (14, 6), (14.5, 13.5)], [(7, 8), (10, 3.5), (11.5, 4.3), (9, 8.5)])
    lump([(4, 14.5), (4.5, 11), (8, 10), (11, 11.5), (11.5, 14.5)], [(4.5, 11), (8, 10), (8.5, 11), (6, 12)])
    I.pixel(9.5, 5, "grey", 7)
    I.pixel(3.5, 8.5, "grey", 6)
    I.pixel(7.5, 11, "grey", 7)


@icon("res_fish", G, "Fish")
def _(I):
    fish(I, 7.5, 8.5, 1.0)


@icon("res_steel", G, "Steel")
def _(I):
    def shape(dx, dy, c, s):
        I.rect(2.5 + dx, 3.5 + dy, 9.5, 3, c, s)
        I.rect(5.5 + dx, 6.5 + dy, 3.5, 3.5, c, s)
        I.rect(2.5 + dx, 10 + dy, 9.5, 3, c, s)
    for i in range(6, 0, -1):
        shape(i * 0.5, -i * 0.4, "blue", 2)
    shape(0, 0, "blue", 4)
    I.rect(2.5, 3.5, 9.5, 1, "blue", 6)
    I.rect(2.5, 10, 9.5, 1, "blue", 6)
    I.rect(5.5, 6.5, 1, 3.5, "blue", 5)
    I.rect(2.5, 5.7, 9.5, 0.8, "blue", 3)
    I.rect(2.5, 12.2, 9.5, 0.8, "blue", 3)


@icon("res_minerals", G, "Minerals")
def _(I):
    def crystal(x, base, h, w, ramp):
        I.poly([(x - w, base), (x - w, base - h * 0.7), (x, base - h), (x + w, base - h * 0.7), (x + w, base)], ramp, 5)
        I.poly([(x, base), (x, base - h), (x + w, base - h * 0.7), (x + w, base)], ramp, 3)
        I.poly([(x - w, base - h * 0.7), (x, base - h), (x - w * 0.3, base - h * 0.55)], ramp, 7)
        I.rect(x - w + 0.5, base - h * 0.5, 0.8, h * 0.4, ramp, 7)
    crystal(4.5, 14, 7, 2.5, "teal")
    crystal(11.5, 14, 8, 2.5, "teal")
    crystal(8, 14.5, 12, 3.2, "purple")


@icon("res_chemicals", G, "Chemicals")
def _(I):
    I.poly([(6.3, 6), (9.7, 6), (14, 14), (2, 14)], "blue", 7)
    I.rect(6.3, 2.5, 3.4, 4, "blue", 7)
    I.poly([(4.8, 10), (11.2, 10), (14, 14), (2, 14)], "green", 5)
    I.poly([(4.8, 10), (8, 10), (5, 12), (3, 14), (2, 14)], "green", 6)
    I.rect(5.5, 1.5, 5, 1.5, "blue", 5)
    I.pixel(7, 7.5, "blue", 5)
    I.pixel(9, 11.5, "green", 7)
    I.pixel(6.5, 12.3, "green", 7)
    I.pixel(10, 12.8, "green", 7)
    I.rect(6.3, 3, 0.8, 3.5, "blue", 5)


@icon("res_pharmaceuticals", G, "Pharmaceuticals")
def _(I):
    X, Y = I.X, I.Y
    axis = X - Y
    perp = (X + Y - 16) / math.sqrt(2)
    def cap(t):
        L2 = 8 ** 2 * 2
        u = np.clip(((X - 4) * 8 + (Y - 12) * -8) / L2, 0, 1)
        return np.sqrt((X - 4 - u * 8) ** 2 + (Y - 12 + u * 8) ** 2) <= t / 2.0
    full = cap(6)
    for half, ramp in ((axis < 0, "red"), (axis >= 0, "grey")):
        m = full & half
        base, lo, hi = (5, 3, 6) if ramp == "red" else (6, 4, 7)
        I._paint(m, ramp, base)
        I._paint(m & (perp > 1.0), ramp, lo)
        I._paint(m & (perp < -1.2) & (perp > -2.2), ramp, hi)
    I._paint(full & (np.abs(axis) < 0.6), "grey", 1)


@icon("res_conveniencefood", G, "Convenience food")
def _(I):
    I.ellipse(8, 8.5, 6.5, 5.3, "orange", 5)
    I.ellipse(7.3, 7.6, 5, 3.7, "orange", 6)
    I.rect(1.5, 8.5, 13, 3, "orange", 5)
    for x, y in ((5, 5), (8, 4), (10.5, 5.8), (7, 6.5)):
        I.pixel(x, y, "yellow", 7)
    I.poly([(1.5, 9), (14.5, 9), (14.5, 10), (13, 10.8), (11.5, 10), (10, 10.8), (8.5, 10), (7, 10.8), (5.5, 10),
            (4, 10.8), (2.5, 10), (1.5, 10.5)], "green", 5)
    I.rect(2, 10.5, 12, 2.2, "brown", 3)
    I.rect(2, 10.5, 12, 0.7, "brown", 4)
    I.poly([(3.5, 9.5), (9, 9.5), (7, 12), (5, 11)], "yellow", 5)
    I.rect(2.5, 12.7, 11, 2, "orange", 5)
    I.rect(2.5, 14, 11, 0.7, "orange", 3)


@icon("res_paper", G, "Paper")
def _(I):
    I.isobox(8, 14.5, 7, 6, 4.5, "grey", 7, 7, 5)
    for z in (1.5, 3.0):
        I.isoquad(8, 14.5, [(-7, 0, z), (0, 0, z), (0, 0, z + 0.4), (-7, 0, z + 0.4)], "grey", 5)
        I.isoquad(8, 14.5, [(0, 0, z), (0, -6, z), (0, -6, z + 0.4), (0, 0, z + 0.4)], "grey", 3)
    for y in (-1.5, -3.2):
        I.isoquad(8, 14.5, [(-1.2, y, 4.5), (-6, y, 4.5), (-6, y - 0.8, 4.5), (-1.2, y - 0.8, 4.5)], "blue", 4)
    I.isoquad(8, 14.5, [(-1.2, -4.8, 4.5), (-3.5, -4.8, 4.5), (-3.5, -5.4, 4.5), (-1.2, -5.4, 4.5)], "red", 4)


@icon("res_telecom", G, "Telecom")
def _(I):
    I.poly([(8, 6), (11.5, 14.5), (4.5, 14.5)], "grey", 5)
    I.poly([(8, 6), (11.5, 14.5), (8, 14.5)], "grey", 3)
    I.poly([(6.8, 10), (9.2, 10), (10, 13), (6, 13)], None)
    I.line(6.2, 12.8, 9.8, 10.2, "grey", 3, 0.7)
    I.line(9.8, 12.8, 6.2, 10.2, "grey", 3, 0.7)
    I.ellipse(8, 5, 1.9, 1.9, "red", 5)
    I.pixel(7.3, 4.2, "red", 7)
    for r in (4.6, 7):
        arc(I, 8, 5, r, 1.5, "teal", 5, 140, 215)
        arc(I, 8, 5, r, 1.5, "teal", 5, -35, 40)


@icon("res_lodging", G, "Lodging")
def _(I):
    I.rect(1.5, 4, 2, 10.5, "brown", 4)
    I.rect(1.5, 4, 0.8, 10.5, "brown", 6)
    I.rect(13, 7.5, 1.7, 7, "brown", 3)
    I.rect(1.5, 11, 13.2, 2, "brown", 5)
    I.rect(3.5, 8.5, 9.5, 2.8, "#ffffff")
    I.ellipse(5.5, 8.2, 2, 1.4, "#ffffff")
    I.ellipse(5.2, 7.8, 1.2, 0.7, "grey", 7)
    I.rect(7, 7.8, 6, 3.8, "red", 5)
    I.rect(7, 7.8, 6, 1, "red", 6)
    I.rect(11.5, 7.8, 1.5, 3.8, "red", 3)


@icon("res_recreation", G, "Recreation")
def _(I):
    cx, cy, r = 8, 8, 6.5
    X, Y = I.X, I.Y
    d = np.sqrt((X - cx) ** 2 + (Y - cy) ** 2)
    ball_m = d <= r
    inner = np.sqrt((X - cx + 1.6) ** 2 + (Y - cy + 1.6) ** 2) <= r - 0.6
    ang = (np.degrees(np.arctan2(Y - cy, X - cx)) + 360 + 20) % 360
    cols = ("red", "yellow", "blue", "grey", "red", "yellow")
    for i, c in enumerate(cols):
        sec = ball_m & (ang >= i * 60) & (ang < (i + 1) * 60)
        sh = 7 if c == "grey" else 5
        I._paint(sec, c, sh - 2)
        I._paint(sec & inner, c, sh)
    I.ellipse(5.3, 5, 1.5, 1.1, "grey", 7)
    I.ellipse(cx, cy, 1, 1, "grey", 7)


# ---- natural resource map kinds (iso tiles) -----------------------------------------------------------
@icon("nat_fertile", G, "Fertile land")
def _(I):
    I.isotile(8, 15, 7, 7, "brown", 3, 1.5, "brown")
    for k in (-1.75, -3.5, -5.25):
        I.isoquad(8, 15, [(0, k, 1.5), (-7, k, 1.5), (-7, k - 0.7, 1.5), (0, k - 0.7, 1.5)], "brown", 1)
        I.isoquad(8, 15, [(0, k + 0.7, 1.5), (-7, k + 0.7, 1.5), (-7, k, 1.5), (0, k, 1.5)], "brown", 4)
    I.line(8, 10.5, 8, 6.5, "green", 5, 1.0)
    I.poly([(8, 7.5), (4, 5), (3.5, 7.5), (6, 8.8)], "green", 6)
    I.poly([(8, 7), (12, 4), (12.5, 6.8), (9.5, 8.5)], "green", 5)
    I.pixel(5, 6, "green", 7)


@icon("nat_forest", G, "Forest")
def _(I):
    grass_tile(I, l=6)
    I.rect(7.1, 9, 1.8, 4.5, "brown", 3)
    I.poly([(8, 5.5), (3.5, 11), (12.5, 11)], "green", 4)
    I.poly([(8, 5.5), (3.5, 11), (8, 11)], "green", 5)
    I.poly([(8, 3), (4.5, 8), (11.5, 8)], "green", 4)
    I.poly([(8, 3), (4.5, 8), (8, 8)], "green", 6)
    I.poly([(8, 1.2), (5.5, 5), (10.5, 5)], "green", 5)
    I.poly([(8, 1.2), (5.5, 5), (8, 5)], "green", 7)


@icon("nat_ore", G, "Ore deposit")
def _(I):
    I.isotile(8, 15, 7, 7, "grey", 4, 1.5, "brown")
    I.poly([(3.5, 11.5), (4, 8.5), (7, 6), (10, 6.5), (12.5, 9), (12.5, 11.5), (8, 12.5)], "grey", 5)
    I.poly([(4, 8.5), (7, 6), (10, 6.5), (8, 9), (5, 10)], "grey", 7)
    I.poly([(12.5, 9), (12.5, 11.5), (8, 12.5), (9.5, 9.5)], "grey", 3)
    for x, y, c, s in ((5, 10.5, "orange", 5), (8.5, 9.5, "yellow", 6), (10.5, 10.5, "orange", 6), (7, 7.2, "orange", 5)):
        I.rect(x, y, 1.6, 1.6, c, s)
    I.rect(2.5, 12, 1.8, 1.2, "grey", 6)
    I.rect(12, 12.3, 1.8, 1.2, "grey", 5)


@icon("nat_oil", G, "Oil field")
def _(I):
    I.isotile(8, 15, 7, 7, "sand", 5, 1.5, "brown")
    I.ellipse(8, 11, 5.2, 2.6, "grey", 1)
    I.ellipse(7.7, 10.7, 4.6, 2.2, "grey", 2)
    I.ellipse(6.3, 10, 1.8, 0.8, "purple", 4)
    I.ellipse(9.5, 11.7, 1.4, 0.6, "blue", 4)
    I.pixel(5.5, 9.7, "grey", 7)
    drop(I, 8, 6, 1.9, "grey", 2)
    I.ellipse(7.3, 5.3, 0.6, 0.9, "grey", 6)


@icon("nat_stone", G, "Stone deposit")
def _(I):
    I.isotile(8, 15, 7, 7, "sand", 5, 1.5, "brown")
    ball(I, 9.5, 8.5, 3.5, "grey", 5, 3)
    ball(I, 5.5, 10.5, 3, "grey", 5, 2.5)
    ball(I, 11.2, 11.8, 2.2, "grey", 5, 1.7)
    I.line(8.2, 6, 10.5, 8.5, "grey", 3, 0.5)


@icon("nat_fish", G, "Fish stocks")
def _(I):
    I.isotile(8, 15, 7, 7, "blue", 5, 1.5, "blue")
    for x0, y0, x1, y1 in ((3.5, 11.2, 5.5, 12.2), (10.5, 12.2, 12.5, 11.2), (5.5, 7.2, 7, 6.5)):
        I.line(x0, y0, x1, y1, "blue", 7, 0.7)
    fish(I, 8, 9.2, 0.75)
