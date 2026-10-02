"""Reference icons + shared motifs for the RCT2-style OpenCity icon set (see iso_ui_icon_dsl)."""

from iso_ui_icon_dsl import icon


# ---- shared motifs (other icon files import these) ---------------------------------------------------
def grass_tile(I, fx=8, fy=15, l=7, ramp="green", shade=5):
    I.isotile(fx, fy, l, l, ramp, shade, 1.5, "brown")


def bolt(I, x, y, sc=1.0, c="yellow", s=6):
    pts = [(3, 0), (7, 0), (5, 3.5), (8, 3.5), (2, 10), (3.5, 5), (1, 5)]
    I.poly([(x + px * sc, y + py * sc) for px, py in pts], c, s)


def drop(I, cx, cy, r, c="blue", s=5):
    I.poly([(cx, cy - r * 1.9), (cx + r * 0.9, cy - r * 0.2), (cx - r * 0.9, cy - r * 0.2)], c, s)
    I.ellipse(cx, cy, r, r, c, s)
    I.ellipse(cx - r * 0.35, cy - r * 0.2, r * 0.3, r * 0.4, c, 7)


def person(I, cx, by, h=9, body="blue", skin="#f0b888", bs=5):
    """Simple standing figure; by = feet y, h = total height."""
    hr = h * 0.17
    I.ellipse(cx, by - h + hr, hr, hr, skin)
    I.poly([(cx - h * 0.3, by - h * 0.18), (cx - h * 0.25, by - h + 2.2 * hr), (cx + h * 0.25, by - h + 2.2 * hr),
            (cx + h * 0.3, by - h * 0.18)], body, bs)
    I.rect(cx - h * 0.22, by - h * 0.2, h * 0.44, h * 0.2, "grey", 2)


def lens(I, ramp="teal"):
    """Info-view lens: a round coloured disc; symbols go on top."""
    I.ellipse(8, 8, 7, 7, ramp, 3)
    I.ellipse(8, 8, 6, 6, ramp, 4)
    I.ellipse(7, 7, 4.5, 4.5, ramp, 5)


BADGE = {"critical": "red", "warning": "orange", "info": "blue", "good": "green", "neutral": "grey"}


def badge(I, level="critical"):
    """Status/problem badge: a rounded speech-bubble pin with a pointer at the bottom.
    Shared shape with the world-space status icons (LIFE). Symbol area: x 3..13, y 2..11."""
    r = BADGE[level]
    I.poly([(6, 12), (10, 12), (8, 15)], r, 3)
    I.ellipse(8, 7, 7, 6.5, r, 3)
    I.ellipse(8, 7, 6, 5.5, r, 4)
    I.ellipse(7.3, 6, 4.2, 3.4, r, 5)


# ---- reference icons ----------------------------------------------------------------------------------
@icon("cat_roads", "build", "Roads")
def _(I):
    grass_tile(I)
    I.isoquad(8, 15, [(0, -2.5, 1.5), (-7, -2.5, 1.5), (-7, -4.5, 1.5), (0, -4.5, 1.5)], "grey", 3)
    I.isoquad(8, 15, [(-1, -3.4, 1.5), (-2.5, -3.4, 1.5), (-2.5, -3.6, 1.5), (-1, -3.6, 1.5)], "yellow", 6)
    I.isoquad(8, 15, [(-4, -3.4, 1.5), (-5.5, -3.4, 1.5), (-5.5, -3.6, 1.5), (-4, -3.6, 1.5)], "yellow", 6)
    I.line(4.5, 9.2, 11.5, 12.7, "yellow", 6, 0.6)


@icon("cat_zoning", "build", "Zoning")
def _(I):
    I.isobox(8, 15, 7, 7, 1.5, "brown", 4, 4, 2)
    q = lambda x0, y0, x1, y1, c: I.isoquad(8, 15, [(x0, y0, 1.5), (x1, y0, 1.5), (x1, y1, 1.5), (x0, y1, 1.5)], c, 5)
    q(0, 0, -3.5, -3.5, "green")
    q(-3.5, 0, -7, -3.5, "blue")
    q(0, -3.5, -3.5, -7, "yellow")
    q(-3.5, -3.5, -7, -7, "orange")


@icon("cat_power", "build", "Electricity")
def _(I):
    grass_tile(I, l=6)
    I.isobox(9, 13, 4, 4, 5, "grey", 6, 5, 3)
    I.isobox(9, 13, 2, 2, 9, "red", 5, 4, 2)
    I.rect(6.8, 1, 0.8, 3, "grey", 4)
    bolt(I, 1, 2, 0.75)


@icon("cat_housing", "build", "Residential")
def _(I):
    grass_tile(I)
    I.isobox(9, 14, 6, 5, 5, "sand", 7, 6, 4)
    I.gable(9, 14, 6, 5, 5, 3.5, "red", "x", 5, 3)
    I.isoquad(9, 14, [(-2, 0, 1), (-3, 0, 1), (-3, 0, 3), (-2, 0, 3)], "brown", 3)
    I.isoquad(9, 14, [(-4.5, 0, 2.5), (-5.5, 0, 2.5), (-5.5, 0, 3.5), (-4.5, 0, 3.5)], "blue", 6)


@icon("time_pause", "time", "Pause")
def _(I):
    I.rect(4, 3, 3, 10, "grey", 7)
    I.rect(9, 3, 3, 10, "grey", 7)
    I.rect(6, 3, 1, 10, "grey", 5)
    I.rect(11, 3, 1, 10, "grey", 5)


def _tri(I, x, w, s=7, c="green"):
    I.poly([(x, 3), (x + w, 8), (x, 13)], c, s)


@icon("time_play", "time", "Normal speed")
def _(I):
    _tri(I, 5, 7, 5)
    I.poly([(5, 3), (12, 8), (5, 8)], "green", 6)


@icon("time_fast", "time", "Fast (x2)")
def _(I):
    _tri(I, 2, 6, 5); _tri(I, 8, 6, 5)
    I.poly([(2, 3), (8, 8), (2, 8)], "green", 6); I.poly([(8, 3), (14, 8), (8, 8)], "green", 6)


@icon("time_fastest", "time", "Fastest (x3)")
def _(I):
    for x in (1, 5.5, 10):
        _tri(I, x, 4.5, 5, "orange")
        I.poly([(x, 3), (x + 4.5, 8), (x, 8)], "orange", 6)


@icon("stat_money", "stats", "Money")
def _(I):
    for i, y in enumerate((13, 11, 9)):
        I.ellipse(7, y, 5, 2, "yellow", 3)
        I.ellipse(7, y - 1, 5, 2, "yellow", 5)
    I.ellipse(10.5, 7, 4, 4, "yellow", 4)
    I.ellipse(10, 6.5, 3, 3, "yellow", 6)
    I.text(9, 4.5, "$", "yellow", 2)


@icon("stat_population", "stats", "Population")
def _(I):
    person(I, 5, 14, 10, "blue")
    person(I, 11, 14, 11, "red")


@icon("stat_happiness", "stats", "Happiness")
def _(I):
    I.ellipse(8, 8, 6.5, 6.5, "yellow", 4)
    I.ellipse(7.5, 7.5, 5.5, 5.5, "yellow", 6)
    I.rect(5, 5, 1.2, 2.2, "brown", 1); I.rect(9.8, 5, 1.2, 2.2, "brown", 1)
    I.poly([(4, 9), (12, 9), (10.5, 11.5), (5.5, 11.5)], "brown", 1)
    I.rect(6, 10, 4, 1.2, "red", 5)


@icon("info_electricity", "infoview", "Electricity")
def _(I):
    lens(I)
    bolt(I, 4.3, 3, 0.95)


@icon("st_no_power", "status", "No electricity")
def _(I):
    badge(I, "critical")
    bolt(I, 5, 2.2, 0.85, "yellow", 6)


@icon("st_no_water", "status", "No water")
def _(I):
    badge(I, "critical")
    drop(I, 8, 8, 2.6, "blue", 5)
