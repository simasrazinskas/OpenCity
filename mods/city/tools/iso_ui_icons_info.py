"""OpenCity RCT2-style icons, group "infoview": one lens icon per info view (map overlay mode).

Every icon is a round coloured disc (the shared lens motif) with a light, saturated symbol on top.
The disc colour encodes the family: services = teal, networks = blue, environment = green, city = purple.
"""

import math

from iso_ui_icon_dsl import icon
from iso_ui_icons_core import bolt, drop

FAM = {"services": "teal", "networks": "blue", "environment": "green", "city": "purple"}
WH = ("grey", 7)  # near-white


# ---- local motifs ------------------------------------------------------------------------------------
def disc(I, fam):
    """Info-view lens: dark rim, mid body, lit upper-left. Symbol area is roughly x 3.5..12.5, y 3.5..12.5."""
    r = FAM[fam]
    I.ellipse(8, 8, 7, 7, r, 2)
    I.ellipse(8, 8, 6.2, 6.2, r, 3)
    I.ellipse(7.4, 7.4, 5.2, 5.2, r, 4)


def arc(I, cx, cy, r, a0, a1, t, c, s, steps=10):
    """Arc made of line segments; angles in degrees, 0 = right, 90 = down."""
    pts = [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / steps)),
            cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / steps))) for i in range(steps + 1)]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        I.line(x0, y0, x1, y1, c, s, t)


def star(I, cx, cy, r, c="yellow", s=6):
    pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        rr = r if i % 2 == 0 else r * 0.45
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    I.poly(pts, c, s)


def dollar(I, x, y, c="yellow", s=2, stick=1):
    """Pixel '$' 3 wide, 5 tall (+1 stick top and bottom); (x, y) = top-left of the S."""
    I.rect(x, y, 3, 1, c, s)
    I.rect(x, y + 1, 1, 1, c, s)
    I.rect(x, y + 2, 3, 1, c, s)
    I.rect(x + 2, y + 3, 1, 1, c, s)
    I.rect(x, y + 4, 3, 1, c, s)
    I.rect(x + 1, y - stick, 1, 5 + 2 * stick, c, s)


def lit_drop(I, cx, cy, r, c="blue", s=6, edge=1):
    """Drop with a dark edge so it reads on any disc."""
    drop(I, cx, cy + 0.1, r + 0.8, c, edge)
    drop(I, cx, cy, r, c, s)


# ---- services (teal) -----------------------------------------------------------------------------------
@icon("info_electricity", "infoview", "Electricity")
def _(I):
    disc(I, "services")
    bolt(I, 4.3, 3, 0.95)


@icon("info_power", "infoview", "Electricity")
def _(I):
    disc(I, "services")
    bolt(I, 4.3, 3, 0.95)


@icon("info_water", "infoview", "Water")
def _(I):
    disc(I, "services")
    lit_drop(I, 8, 9.2, 3.1, "blue", 6)


@icon("info_garbage", "infoview", "Garbage")
def _(I):
    disc(I, "services")
    I.poly([(5, 6.5), (11, 6.5), (10.3, 12), (5.7, 12)], "grey", 6)
    I.rect(7.2, 7.5, 0.8, 3.8, "grey", 3)
    I.rect(9, 7.5, 0.8, 3.8, "grey", 3)
    I.rect(4.3, 5, 7.4, 1.6, "grey", 7)
    I.rect(6.8, 3.9, 2.4, 1.1, "grey", 7)


@icon("info_health", "infoview", "Health care")
def _(I):
    disc(I, "services")
    I.rect(6, 3.5, 4, 9, WH[0], 7)
    I.rect(3.5, 6, 9, 4, WH[0], 7)
    I.rect(6.8, 4.3, 2.4, 7.4, "red", 5)
    I.rect(4.3, 6.8, 7.4, 2.4, "red", 5)


@icon("info_deathcare", "infoview", "Death care")
def _(I):
    disc(I, "services")
    I.ellipse(8, 6.8, 2.6, 2.6, "grey", 6)
    I.rect(5.4, 6.8, 5.2, 5.5, "grey", 6)
    I.rect(5.4, 6.8, 1, 5.5, "grey", 7)
    I.rect(7.5, 5.2, 1, 4, "grey", 2)
    I.rect(6.5, 6.4, 3, 1, "grey", 2)
    I.rect(3.8, 12.2, 8.4, 1.2, "brown", 4)


@icon("info_education", "infoview", "Education")
def _(I):
    disc(I, "services")
    I.poly([(5.4, 8), (10.6, 8), (10.6, 11), (8, 12.4), (5.4, 11)], "purple", 4)
    I.rect(5.4, 8, 1, 3, "purple", 5)
    I.poly([(8, 3.8), (13.4, 6.6), (8, 9.4), (2.6, 6.6)], WH[0], 7)
    I.poly([(8, 5), (11, 6.6), (8, 8.2), (5, 6.6)], "purple", 5)
    I.line(12.2, 7, 12.2, 10.8, "yellow", 6, 1)


@icon("info_police", "infoview", "Police")
def _(I):
    disc(I, "services")
    I.poly([(4.3, 3.8), (11.7, 3.8), (11.7, 9), (8, 12.8), (4.3, 9)], WH[0], 7)
    I.poly([(5.4, 4.9), (10.6, 4.9), (10.6, 8.5), (8, 11.2), (5.4, 8.5)], "blue", 4)
    star(I, 8, 7.8, 2.6, "yellow", 6)


@icon("info_crime", "infoview", "Crime")
def _(I):
    disc(I, "services")
    I.ellipse(8, 8.3, 3.7, 4.2, "#f0b888")
    I.rect(4.2, 6.3, 7.6, 2.4, "grey", 1)
    I.rect(5.6, 7, 1.6, 1, WH[0], 7)
    I.rect(8.8, 7, 1.6, 1, WH[0], 7)
    I.rect(6.2, 10.4, 3.6, 0.9, "red", 4)
    I.poly([(4.5, 5.8), (11.5, 5.8), (10.5, 3.8), (5.5, 3.8)], "grey", 2)


@icon("info_fire", "infoview", "Fire")
def _(I):
    disc(I, "services")
    I.ellipse(8, 9.6, 3.7, 3.4, "red", 5)
    I.poly([(4.3, 9.2), (8.5, 3.2), (11.7, 9.2)], "red", 5)
    I.ellipse(8, 10.2, 2.6, 2.4, "orange", 6)
    I.poly([(5.6, 10), (8, 6), (10.4, 10)], "orange", 6)
    I.ellipse(8, 11, 1.5, 1.4, "yellow", 7)


@icon("info_parks", "infoview", "Parks")
def _(I):
    disc(I, "services")
    I.rect(7.2, 10, 1.6, 3, "brown", 5)
    I.ellipse(8, 6.6, 3.3, 3, "green", 6)
    I.ellipse(5.9, 8.4, 2.5, 2.3, "green", 6)
    I.ellipse(10.1, 8.4, 2.5, 2.3, "green", 5)
    I.ellipse(7.2, 5.8, 1.6, 1.4, "green", 7)


@icon("info_telecom", "infoview", "Telecom")
def _(I):
    disc(I, "services")
    I.poly([(7.2, 7), (8.8, 7), (9.8, 12.8), (6.2, 12.8)], WH[0], 7)
    I.ellipse(8, 6.2, 1.3, 1.3, "red", 5)
    arc(I, 8, 6.2, 3, 200, 340, 1, "yellow", 6)
    arc(I, 8, 6.2, 4.7, 205, 335, 1, "yellow", 6, 14)


@icon("info_post", "infoview", "Post")
def _(I):
    disc(I, "services")
    I.rect(3.5, 5, 9, 6.6, WH[0], 7)
    I.line(3.8, 5.3, 8, 8.8, "grey", 4, 1)
    I.line(12.2, 5.3, 8, 8.8, "grey", 4, 1)
    I.rect(10, 9.4, 1.8, 1.6, "red", 5)


# ---- networks (blue) -----------------------------------------------------------------------------------
@icon("info_powergrid", "infoview", "Power grid")
def _(I):
    disc(I, "networks")
    I.line(8, 3.4, 6, 12.8, "yellow", 6, 1)
    I.line(8, 3.4, 10, 12.8, "yellow", 6, 1)
    I.line(4, 5.6, 12, 5.6, "yellow", 6, 1)
    I.line(5, 8.2, 11, 8.2, "yellow", 6, 1)
    I.line(6.6, 8.2, 9.4, 12.4, "yellow", 5, 0.8)
    I.line(9.4, 8.2, 6.6, 12.4, "yellow", 5, 0.8)
    I.rect(7, 3, 2, 1.2, WH[0], 7)


def _pipe(I):
    I.rect(3, 10, 10, 2.8, "grey", 6)
    I.rect(3, 10, 10, 0.9, "grey", 7)
    I.rect(3, 10, 1.4, 2.8, "grey", 4)
    I.rect(11.6, 10, 1.4, 2.8, "grey", 4)


@icon("info_watergrid", "infoview", "Water network")
def _(I):
    disc(I, "networks")
    lit_drop(I, 8, 7.2, 2.5, "blue", 7, 2)
    _pipe(I)


@icon("info_sewage", "infoview", "Sewage")
def _(I):
    disc(I, "networks")
    lit_drop(I, 8, 7.2, 2.5, "brown", 5, 1)
    _pipe(I)


@icon("info_roads", "infoview", "Roads")
def _(I):
    disc(I, "networks")
    I.poly([(6.4, 3.6), (9.6, 3.6), (13, 12.6), (3, 12.6)], "grey", 4)
    I.line(6.4, 3.6, 3, 12.6, WH[0], 7, 0.8)
    I.line(9.6, 3.6, 13, 12.6, WH[0], 7, 0.8)
    I.rect(7.6, 4.6, 0.9, 1.6, "yellow", 6)
    I.rect(7.5, 7.4, 1, 2, "yellow", 6)
    I.rect(7.4, 10.6, 1.2, 2, "yellow", 6)


@icon("info_traffic", "infoview", "Traffic")
def _(I):
    disc(I, "networks")
    I.rect(5.6, 3.4, 4.8, 9.4, "grey", 1)
    I.rect(6.2, 4, 3.6, 8.2, "grey", 2)
    I.ellipse(8, 5.7, 1.2, 1.2, "red", 5)
    I.ellipse(8, 8, 1.2, 1.2, "yellow", 6)
    I.ellipse(8, 10.3, 1.2, 1.2, "green", 6)
    I.rect(7.3, 12.8, 1.4, 0.4, "grey", 3)


@icon("info_transit", "infoview", "Public transport")
def _(I):
    disc(I, "networks")
    I.rect(4.5, 4, 7, 8, "yellow", 6)
    I.rect(4.5, 4, 1, 8, "yellow", 7)
    I.rect(10.5, 4, 1, 8, "yellow", 4)
    I.rect(5.4, 4.8, 5.2, 3, "blue", 2)
    I.rect(5.4, 4.8, 5.2, 1.1, "blue", 6)
    I.rect(4.5, 9.8, 7, 0.8, "yellow", 4)
    I.ellipse(6, 10.9, 0.8, 0.8, WH[0], 7)
    I.ellipse(10, 10.9, 0.8, 0.8, WH[0], 7)
    I.rect(4.9, 12, 1.8, 1.2, "grey", 1)
    I.rect(9.3, 12, 1.8, 1.2, "grey", 1)


@icon("info_freight", "infoview", "Freight")
def _(I):
    disc(I, "networks")
    I.rect(3, 5.4, 6.6, 5.8, "orange", 6)
    I.rect(3, 5.4, 6.6, 1, "orange", 7)
    I.rect(3, 8, 6.6, 0.8, "orange", 4)
    I.poly([(9.6, 7), (11.8, 7), (13, 9.2), (13, 11.2), (9.6, 11.2)], "yellow", 6)
    I.rect(10.4, 7.8, 1.4, 1.2, "blue", 3)
    I.rect(3, 11.2, 10, 0.9, "grey", 2)
    for x in (5, 11):
        I.ellipse(x, 12, 1.5, 1.5, "grey", 1)
        I.ellipse(x, 12, 0.6, 0.6, "grey", 6)


# ---- environment (green) -------------------------------------------------------------------------------
@icon("info_pollution", "infoview", "Pollution")
def _(I):
    disc(I, "environment")
    I.ellipse(8, 9.6, 4.8, 2.4, "sand", 3)
    I.ellipse(5.8, 8, 2.6, 2.4, "sand", 5)
    I.ellipse(10.2, 7.8, 3, 2.8, "sand", 5)
    I.ellipse(8, 6, 2.8, 2.6, "sand", 6)
    I.ellipse(8, 9, 4.2, 1.7, "sand", 4)
    I.ellipse(7.4, 5.4, 1.4, 1.1, "sand", 7)
    I.ellipse(5.6, 10.3, 0.9, 0.9, "sand", 2)
    I.ellipse(8.4, 10.6, 0.9, 0.9, "sand", 2)
    I.ellipse(11, 10.2, 0.9, 0.9, "sand", 2)


@icon("info_air", "infoview", "Air pollution")
def _(I):
    disc(I, "environment")
    I.rect(3.5, 9.2, 9, 3.6, "grey", 5)
    I.poly([(3.5, 9.2), (3.5, 7.4), (6.5, 9.2), (6.5, 7.4), (9.5, 9.2)], "grey", 6)
    I.rect(9.6, 5.6, 2, 4, "red", 5)
    I.rect(9.6, 5.6, 0.7, 4, "red", 6)
    I.rect(4.6, 10.4, 1.2, 1, "yellow", 6)
    I.rect(7, 10.4, 1.2, 1, "yellow", 6)
    I.ellipse(10.6, 4.2, 1.4, 1.2, "grey", 7)
    I.ellipse(12, 3.6, 1.1, 1, "grey", 6)
    I.ellipse(8.4, 3.8, 1, 0.9, "grey", 6)


@icon("info_ground", "infoview", "Ground pollution")
def _(I):
    disc(I, "environment")
    I.isotile(8, 13.4, 5.5, 5.5, "sand", 6, 1.4, "brown")
    I.poly([(5.6, 9.2), (7.5, 8.2), (10, 8.6), (11, 9.8), (9.4, 10.8), (7, 10.6)], "brown", 3)
    I.poly([(6.4, 9.2), (7.6, 8.6), (9.2, 8.9), (8, 9.8)], "brown", 4)
    I.pixel(11.4, 11, "brown", 3)
    I.pixel(4.8, 10.6, "brown", 3)
    I.rect(7.4, 3.8, 1.6, 3.8, "brown", 3)
    I.rect(7.9, 4, 0.8, 3.4, "yellow", 5)


@icon("info_noise", "infoview", "Noise")
def _(I):
    disc(I, "environment")
    I.rect(3.6, 6.4, 2.6, 3.6, WH[0], 7)
    I.poly([(6.2, 6.4), (9, 4.2), (9, 12.2), (6.2, 10)], WH[0], 6)
    arc(I, 9, 8.2, 2.4, -55, 55, 1, "yellow", 6)
    arc(I, 9, 8.2, 4, -55, 55, 1, "yellow", 6, 12)


@icon("info_groundwater", "infoview", "Groundwater")
def _(I):
    disc(I, "environment")
    I.rect(3.5, 4.6, 9, 3.4, "brown", 5)
    I.rect(3.5, 4.6, 9, 1.2, "green", 7)
    I.rect(3.5, 7, 9, 1, "brown", 3)
    I.rect(3.5, 8, 9, 4.6, "blue", 5)
    I.rect(3.5, 8, 9, 1, "blue", 7)
    I.rect(5, 10.2, 2, 0.8, "blue", 7)
    I.rect(9, 11.4, 2, 0.8, "blue", 7)


@icon("info_resources", "infoview", "Resources")
def _(I):
    disc(I, "environment")
    I.line(4.8, 12.6, 10.6, 5.6, "brown", 6, 1.6)
    I.poly([(4.8, 5.4), (8.5, 3.6), (12.4, 4.8), (13, 7.6), (11.6, 6.4), (8.5, 5.6), (6, 6.8)], "grey", 7)
    I.poly([(9.4, 11), (11, 9.4), (13, 10.6), (12.4, 12.6), (10, 12.8)], "yellow", 6)
    I.rect(10.4, 10.2, 1.2, 1.2, "yellow", 7)


@icon("info_landvalue", "infoview", "Land value")
def _(I):
    disc(I, "environment")
    I.rect(3.8, 7.6, 6.2, 5, "sand", 7)
    I.poly([(2.8, 8), (6.9, 3.8), (11, 8)], "red", 5)
    I.rect(5.4, 9.8, 1.8, 2.8, "brown", 4)
    I.ellipse(10.2, 10.2, 3.1, 3.1, "yellow", 3)
    I.ellipse(10, 10, 2.6, 2.6, "yellow", 6)
    dollar(I, 8.6, 7.7, "brown", 2, 0)


# ---- city (purple) -------------------------------------------------------------------------------------
@icon("info_happiness", "infoview", "Happiness")
def _(I):
    disc(I, "city")
    I.ellipse(8, 8, 5, 5, "yellow", 3)
    I.ellipse(7.6, 7.6, 4.4, 4.4, "yellow", 6)
    I.rect(5.8, 5.8, 1.2, 2, "brown", 1)
    I.rect(9.2, 5.8, 1.2, 2, "brown", 1)
    arc(I, 8, 7.6, 3, 25, 155, 1.1, "brown", 1, 8)


@icon("info_level", "infoview", "Building level")
def _(I):
    disc(I, "city")
    star(I, 8, 5, 2.6, "yellow", 6)
    I.rect(4.6, 7.6, 6.8, 5.6, "grey", 6)
    I.rect(4.6, 7.6, 1, 5.6, "grey", 7)
    I.rect(10.4, 7.6, 1, 5.6, "grey", 4)
    for x in (6.2, 8.8):
        for y in (8.4, 10.4):
            I.rect(x, y, 1.4, 1.2, "blue", 3)
    I.rect(7.4, 11.8, 1.2, 1.4, "brown", 4)


@icon("info_attainment", "infoview", "Education level")
def _(I):
    disc(I, "city")
    for y, w, c in ((10.6, 9, "red"), (8.2, 8, "yellow"), (5.8, 7, "green")):
        x = 8 - w / 2
        I.rect(x, y, w, 2.2, c, 5)
        I.rect(x, y, w, 0.8, c, 7)
        I.rect(x + w - 1.6, y + 0.8, 1.6, 1.4, WH[0], 7)


@icon("info_wealth", "infoview", "Wealth")
def _(I):
    disc(I, "city")
    I.poly([(6.2, 5.8), (9.8, 5.8), (9.3, 7.4), (6.7, 7.4)], "yellow", 5)
    I.ellipse(8, 10.2, 4.3, 3.2, "yellow", 4)
    I.ellipse(7.4, 9.7, 3.4, 2.5, "yellow", 6)
    I.rect(6.2, 4.6, 3.6, 1.4, "red", 5)
    dollar(I, 6.5, 8.2, "brown", 2)


@icon("info_age", "infoview", "Age")
def _(I):
    disc(I, "city")
    I.poly([(5.2, 4.6), (10.8, 4.6), (8.6, 8), (10.8, 11.8), (5.2, 11.8), (7.4, 8)], "blue", 7)
    I.poly([(6.4, 11.8), (9.6, 11.8), (8, 9.6)], "yellow", 6)
    I.poly([(6.4, 4.6), (9.6, 4.6), (8.4, 6.2)], "yellow", 6)
    I.rect(4.3, 3.4, 7.4, 1.3, "brown", 5)
    I.rect(4.3, 11.8, 7.4, 1.3, "brown", 5)


@icon("info_production", "infoview", "Production")
def _(I):
    disc(I, "city")
    for a in range(0, 180, 45):
        t = math.radians(a)
        I.line(8 - 5 * math.cos(t), 8 - 5 * math.sin(t), 8 + 5 * math.cos(t), 8 + 5 * math.sin(t), "orange", 6, 2)
    I.ellipse(8, 8, 3.8, 3.8, "orange", 6)
    I.ellipse(7.6, 7.6, 2.8, 2.8, "orange", 7)
    I.ellipse(8, 8, 1.5, 1.5, "grey", 2)


@icon("info_tourism", "infoview", "Tourism")
def _(I):
    disc(I, "city")
    I.rect(5.4, 4.6, 3.2, 1.8, "grey", 5)
    I.rect(3.5, 6, 9, 6, "grey", 6)
    I.rect(3.5, 6, 9, 1.1, "grey", 7)
    I.ellipse(8, 9.2, 2.5, 2.5, "grey", 1)
    I.ellipse(8, 9.2, 1.7, 1.7, "blue", 5)
    I.pixel(7.2, 8.4, WH[0], 7)
    I.rect(10.8, 6.8, 1, 1, "yellow", 6)


@icon("info_districts", "infoview", "Districts")
def _(I):
    disc(I, "city")
    I.rect(4.8, 3.6, 1.1, 9.4, WH[0], 7)
    I.poly([(5.9, 4), (12.6, 5.4), (5.9, 8.8)], "red", 5)
    I.poly([(5.9, 4), (12.6, 5.4), (5.9, 6.2)], "red", 6)
    I.rect(3.6, 12.2, 3.6, 1, "grey", 4)
