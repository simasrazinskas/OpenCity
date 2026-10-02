"""Transit + industry-hub icons for the RCT2-style OpenCity icon set (see iso_ui_icon_dsl)."""

from iso_ui_icon_dsl import icon
from iso_ui_icons_core import grass_tile, person


# ---- shared helpers -----------------------------------------------------------------------------------
def wheel(I, cx, cy, r=1.5):
    I.ellipse(cx, cy, r, r, "grey", 1)
    I.ellipse(cx, cy, r * 0.5, r * 0.5, "grey", 5)


def windows(I, x0, x1, y0, y1, n, ramp="blue", s=7, gap=0.6):
    w = (x1 - x0 - gap * (n - 1)) / n
    for i in range(n):
        I.rect(x0 + i * (w + gap), y0, w, y1 - y0, ramp, s)


def sign_post(I, x, y0, y1, c="grey", s=4):
    I.rect(x, y0, 1, y1 - y0, c, s)


def sign(I, x, y, w, h, c="blue", s=5):
    I.rect(x, y, w, h, c, s)
    I.rect(x, y, w, 0.8, c, 7)
    I.rect(x + 1, y + 1.2, max(w - 2, 1), 1, "grey", 7)


def stop_ground(I):
    I.isotile(8, 15, 7, 5, "grey", 5, 1, "grey")


# ---- vehicles (side view, facing right) ---------------------------------------------------------------
@icon("tr_bus", "transit", "Bus")
def _(I):
    I.rect(1.5, 4.5, 13, 7.5, "blue", 5)
    I.rect(1.5, 4.5, 13, 1, "blue", 7)
    I.rect(1.5, 10, 13, 2, "blue", 3)
    windows(I, 2.5, 14, 6, 8.5, 4, "blue", 7)
    I.rect(1.5, 9.2, 13, 0.8, "grey", 7)
    wheel(I, 4.5, 12, 1.6)
    wheel(I, 11.5, 12, 1.6)


@icon("tr_taxi", "transit", "Taxi")
def _(I):
    I.rect(5.5, 3.5, 4, 1.6, "yellow", 2)
    I.rect(6, 3.5, 3, 1, "yellow", 7)
    I.poly([(4.5, 5.2), (10.5, 5.2), (12, 8.5), (3.5, 8.5)], "yellow", 5)
    I.poly([(5.5, 6), (7.5, 6), (7.5, 8), (4.7, 8)], "blue", 7)
    I.poly([(8.5, 6), (10, 6), (11, 8), (8.5, 8)], "blue", 7)
    I.rect(1.5, 8.2, 13, 3.8, "yellow", 5)
    I.rect(1.5, 8.2, 13, 1, "yellow", 7)
    I.rect(1.5, 10.5, 13, 1.5, "yellow", 3)
    I.rect(1.5, 9.8, 13, 0.7, "grey", 1)
    wheel(I, 4.5, 12, 1.6)
    wheel(I, 11.5, 12, 1.6)


@icon("tr_tram", "transit", "Tram")
def _(I):
    I.line(5, 3, 11, 3, "grey", 2, 0.8)
    I.line(8, 3, 8, 5, "grey", 2, 0.8)
    I.rect(1.5, 5, 13, 7, "red", 5)
    I.rect(1.5, 5, 13, 1, "red", 7)
    I.rect(1.5, 10, 13, 2, "red", 3)
    I.rect(1.5, 6.5, 13, 3.5, "sand", 7)
    windows(I, 2.5, 14, 7, 9.2, 4, "blue", 6)
    wheel(I, 4, 12.5, 1.3)
    wheel(I, 12, 12.5, 1.3)


@icon("tr_metro", "transit", "Metro")
def _(I):
    I.rect(1.5, 4.5, 13, 7.5, "grey", 6)
    I.rect(1.5, 4.5, 13, 1, "grey", 7)
    I.rect(1.5, 10, 13, 2, "grey", 3)
    I.rect(1.5, 9, 13, 1.2, "blue", 4)
    windows(I, 2.5, 8, 6, 8.5, 2, "blue", 6)
    I.rect(9.5, 5.5, 5, 3.5, "blue", 5)
    for a in ((10.4, 8.4, 10.4, 5.8), (10.4, 5.8, 12, 7.6), (12, 7.6, 13.6, 5.8), (13.6, 5.8, 13.6, 8.4)):
        I.line(*a, "#ffffff", 7, 0.9)
    wheel(I, 4.5, 12.5, 1.3)
    wheel(I, 11.5, 12.5, 1.3)


@icon("tr_train", "transit", "Train")
def _(I):
    I.rect(1.5, 4.5, 13, 7, "green", 5)
    I.rect(1.5, 4.5, 13, 1, "green", 7)
    I.rect(1.5, 9.8, 13, 1.7, "red", 4)
    windows(I, 2.5, 14, 6, 8.5, 4, "blue", 7)
    I.rect(1.5, 9.2, 13, 0.7, "yellow", 6)
    I.rect(1.5, 3, 4, 2, "grey", 3)
    wheel(I, 4, 12.5, 1.3)
    wheel(I, 8, 12.5, 1.3)
    wheel(I, 12, 12.5, 1.3)


@icon("tr_cargo_train", "transit", "Freight train")
def _(I):
    I.rect(1.5, 4, 13, 7.5, "orange", 4)
    I.rect(1.5, 4, 13, 1, "orange", 6)
    for x in (4.5, 8, 11.5):
        I.rect(x, 4.5, 0.8, 7, "orange", 2)
    I.rect(1.5, 9.5, 13, 2, "orange", 3)
    I.rect(0.8, 11.5, 14.4, 1, "grey", 2)
    wheel(I, 3.5, 12.8, 1.2)
    wheel(I, 12.5, 12.8, 1.2)


@icon("tr_truck", "transit", "Truck")
def _(I):
    I.rect(1.5, 3.5, 8.5, 8, "sand", 7)
    I.rect(1.5, 3.5, 8.5, 1, "sand", 7)
    I.rect(1.5, 9, 8.5, 2.5, "sand", 4)
    I.rect(6.2, 3.5, 0.8, 5.5, "sand", 4)
    I.poly([(10.5, 5.5), (13, 5.5), (14.5, 8.5), (14.5, 11.5), (10.5, 11.5)], "red", 5)
    I.poly([(11.2, 6.2), (12.8, 6.2), (13.8, 8.5), (11.2, 8.5)], "blue", 7)
    I.rect(10.5, 10, 4, 1.5, "red", 3)
    wheel(I, 4, 12, 1.6)
    wheel(I, 12, 12, 1.6)


@icon("tr_ship", "transit", "Cargo ship")
def _(I):
    I.rect(3.5, 3.8, 3, 2.4, "orange", 5)
    I.rect(2, 6.2, 3.5, 2.8, "blue", 5)
    I.rect(5.5, 6.2, 3.5, 2.8, "red", 5)
    I.rect(2, 6.2, 7, 0.7, "blue", 7)
    I.rect(10, 3.5, 3.5, 5.5, "grey", 7)
    I.rect(10, 4.8, 3.5, 1.2, "blue", 6)
    I.rect(10, 3.5, 3.5, 0.8, "red", 5)
    I.poly([(1, 9), (15, 9), (13, 12), (3, 12)], "grey", 3)
    I.rect(1, 9, 14, 1, "red", 5)
    I.rect(1.5, 12.5, 13, 2, "blue", 5)
    I.rect(1.5, 12.5, 13, 0.8, "blue", 7)


@icon("tr_plane", "transit", "Cargo plane")
def _(I):
    I.ellipse(8, 8, 7, 2.4, "grey", 6)
    I.poly([(11, 8), (15, 8), (15, 5), (14, 4.5)], "grey", 5)
    I.poly([(5, 8), (9, 8), (6, 13), (4, 13)], "grey", 4)
    I.poly([(3, 7), (1.5, 4), (3.2, 4), (5.5, 7)], "grey", 4)
    I.rect(11.6, 6.4, 2, 1, "blue", 7)
    I.rect(4, 8.2, 6, 0.8, "red", 5)
    I.ellipse(7.5, 10.8, 1, 1, "grey", 2)


@icon("tr_bicycle", "transit", "Bicycle")
def _(I):
    I.ring(4, 10.5, 3.2, 1.2, "grey", 2)
    I.ring(12, 10.5, 3.2, 1.2, "grey", 2)
    I.line(4, 10.5, 7.5, 6.5, "red", 5, 1.2)
    I.line(7.5, 6.5, 12, 10.5, "red", 5, 1.2)
    I.line(4, 10.5, 8, 10.5, "red", 4, 1.2)
    I.line(7.5, 6.5, 8, 10.5, "red", 4, 1)
    I.line(12, 10.5, 11, 5.5, "grey", 4, 1)
    I.rect(10, 4.5, 3, 1, "grey", 3)
    I.rect(5.8, 5, 3, 1.2, "brown", 3)


@icon("tr_pedestrian", "transit", "Pedestrian")
def _(I):
    I.ellipse(8, 3.3, 2.3, 2.3, "#f0b888")
    I.poly([(6, 6), (10, 6), (10.5, 10), (5.5, 10)], "orange", 5)
    I.rect(6, 6, 1.5, 4, "orange", 6)
    I.poly([(6.5, 10), (8.2, 10), (6.5, 14.5), (4.8, 14.5)], "blue", 3)
    I.poly([(8, 10), (9.5, 10), (11.5, 14.5), (9.8, 14.5)], "blue", 4)
    I.line(5.5, 6.5, 4.5, 9.5, "#f0b888", 5, 1.2)


@icon("tr_car", "transit", "Car")
def _(I):
    I.poly([(4.5, 5.5), (10, 5.5), (12, 8.5), (3.5, 8.5)], "red", 5)
    I.poly([(5.3, 6.2), (7.5, 6.2), (7.5, 8.2), (4.6, 8.2)], "blue", 7)
    I.poly([(8.3, 6.2), (9.8, 6.2), (11, 8.2), (8.3, 8.2)], "blue", 7)
    I.rect(1.5, 8.2, 13, 3.8, "red", 5)
    I.rect(1.5, 8.2, 13, 1, "red", 7)
    I.rect(1.5, 10.5, 13, 1.5, "red", 3)
    I.rect(13.5, 9, 1, 1.2, "yellow", 7)
    wheel(I, 4.5, 12, 1.6)
    wheel(I, 11.5, 12, 1.6)


# ---- stops, stations, infrastructure (iso mini objects) -----------------------------------------------
def _ground(I, ramp="grey", sh=5, l=6):
    I.isotile(8, 15, l, l, ramp, sh, 1, "grey")


def _shelter(I, roof="blue"):
    """Little glass shelter on the back-left of a stop tile."""
    I.isobox(6.5, 11.5, 4, 2, 3.5, "teal", 7, 6, 4)
    I.isobox(6.5, 8.5, 4.6, 2.6, 0.9, roof, 6, 5, 3)


def _stop_sign(I, x, c, top=2):
    I.rect(x, top + 2, 1, 9.5 - top, "grey", 4)
    I.rect(x - 1.5, top, 4, 3.4, c, 5)
    I.rect(x - 1.5, top, 4, 0.8, c, 7)
    I.rect(x - 0.7, top + 1.4, 2.4, 1, "#ffffff")


@icon("tr_bus_stop", "transit", "Bus stop")
def _(I):
    _ground(I)
    _shelter(I)
    _stop_sign(I, 12, "blue")


@icon("tr_tram_stop", "transit", "Tram stop")
def _(I):
    _ground(I)
    I.isoquad(8, 15, [(-0.5, -3.5, 1), (-5.5, -3.5, 1), (-5.5, -4.1, 1), (-0.5, -4.1, 1)], "grey", 7)
    I.isoquad(8, 15, [(-0.5, -1.5, 1), (-5.5, -1.5, 1), (-5.5, -2.1, 1), (-0.5, -2.1, 1)], "grey", 7)
    _shelter(I, "red")
    _stop_sign(I, 12, "red")


@icon("tr_taxi_stand", "transit", "Taxi stand")
def _(I):
    _ground(I, "grey", 4)
    I.isoquad(8, 15, [(-0.8, -0.8, 1), (-5.2, -0.8, 1), (-5.2, -1.2, 1), (-0.8, -1.2, 1)], "yellow", 6)
    I.isobox(9, 13, 6, 3, 2, "yellow", 6, 5, 3)
    I.isobox(8, 11.3, 3, 2, 1.8, "blue", 7, 6, 4)
    I.isoquad(8, 11.3, [(0, 0, 1.8), (-3, 0, 1.8), (-3, -2, 1.8), (0, -2, 1.8)], "yellow", 7)
    I.rect(12.5, 4.5, 1, 7, "grey", 4)
    I.rect(11, 2, 4, 3, "yellow", 5)
    I.rect(11, 2, 4, 0.8, "yellow", 7)
    I.rect(11.7, 3.4, 2.6, 0.7, "grey", 1)


@icon("tr_station", "transit", "Train station")
def _(I):
    grass_tile(I)
    I.rect(5.5, 2.2, 2, 7, "sand", 6)
    I.rect(7, 2.2, 0.5, 7, "sand", 4)
    I.ellipse(6.5, 3.8, 1.1, 1.1, "#ffffff")
    I.isobox(10.5, 11.5, 5, 3, 3.5, "sand", 7, 6, 4)
    I.isobox(10.5, 8.9, 5.8, 3.8, 1, "red", 6, 5, 3)
    I.isoquad(10.5, 11.5, [(-1, 0, 0.3), (-2.2, 0, 0.3), (-2.2, 0, 2.2), (-1, 0, 2.2)], "brown", 3)
    I.isoquad(10.5, 11.5, [(-3, 0, 1), (-4.2, 0, 1), (-4.2, 0, 2.4), (-3, 0, 2.4)], "blue", 7)
    I.isobox(7.5, 14.3, 5.5, 2, 2.2, "blue", 6, 5, 3)
    I.isoquad(7.5, 14.3, [(-0.8, 0, 1), (-2.4, 0, 1), (-2.4, 0, 1.9), (-0.8, 0, 1.9)], "#ffffff")
    I.isoquad(7.5, 14.3, [(-3.2, 0, 1), (-4.8, 0, 1), (-4.8, 0, 1.9), (-3.2, 0, 1.9)], "#ffffff")


@icon("tr_metro_entrance", "transit", "Metro entrance")
def _(I):
    _ground(I, "grey", 5, 7)
    I.isobox(8, 12.5, 6, 4, 2, "grey", 6, 5, 3)
    I.isoquad(8, 12.5, [(-1.2, 0, 0.2), (-4.8, 0, 0.2), (-4.8, 0, 1.6), (-1.2, 0, 1.6)], "grey", 1)
    I.isoquad(8, 12.5, [(-1.2, -1, 0), (-4.8, -1, 0), (-4.8, -1, 0.2), (-1.2, -1, 0.2)], "blue", 4)
    I.rect(11.5, 2.5, 1, 7.5, "grey", 4)
    I.rect(8.5, 1.5, 6.5, 4.5, "blue", 5)
    I.rect(8.5, 1.5, 6.5, 0.8, "blue", 7)
    for a in ((9.8, 5.2, 9.8, 2.8), (9.8, 2.8, 11.7, 4.6), (11.7, 4.6, 13.6, 2.8), (13.6, 2.8, 13.6, 5.2)):
        I.line(*a, "#ffffff", 7, 0.9)


@icon("tr_depot", "transit", "Depot")
def _(I):
    grass_tile(I)
    I.isobox(10, 14, 8, 5, 4.5, "grey", 7, 6, 4)
    I.gable(10, 14, 8, 5, 4.5, 2.5, "blue", "x", 5, 3)
    I.isoquad(10, 14, [(-1.5, 0, 0), (-6.5, 0, 0), (-6.5, 0, 3.3), (-1.5, 0, 3.3)], "blue", 3)
    for xx in (-2.5, -3.7, -4.9, -6):
        I.isoquad(10, 14, [(xx, 0, 0.2), (xx - 0.4, 0, 0.2), (xx - 0.4, 0, 3.1), (xx, 0, 3.1)], "blue", 5)
    I.isoquad(10, 14, [(-1.5, 0, 3.3), (-6.5, 0, 3.3), (-6.5, 0, 3.9), (-1.5, 0, 3.9)], "yellow", 6)


@icon("tr_harbor", "transit", "Harbor")
def _(I):
    I.isotile(8, 15, 7, 7, "blue", 5, 1.5, "brown")
    I.isobox(12.5, 12.5, 4, 3.5, 2, "grey", 7, 6, 4)
    I.rect(11, 3, 3, 7.5, "red", 5)
    I.rect(11, 3, 1, 7.5, "red", 7)
    I.rect(12, 4, 1.5, 1.4, "yellow", 6)
    I.rect(2.5, 2, 11.5, 1.8, "red", 5)
    I.rect(2.5, 2, 11.5, 0.7, "red", 7)
    I.line(4.5, 3.8, 4.5, 7.5, "grey", 2, 0.6)
    I.rect(3, 7.5, 3, 2.2, "orange", 5)
    I.rect(3, 7.5, 3, 0.7, "orange", 7)
    I.poly([(2, 10.3), (9, 10.3), (7.8, 12.3), (3.2, 12.3)], "blue", 3)
    I.rect(2, 10.3, 7, 0.8, "blue", 6)


@icon("tr_airport", "transit", "Airport")
def _(I):
    _ground(I, "grey", 3, 7)
    I.isoquad(8, 15, [(-1, -3, 1), (-6, -3, 1), (-6, -4, 1), (-1, -4, 1)], "yellow", 6)
    I.isobox(9, 12, 2.5, 2.5, 5, "grey", 7, 6, 4)
    I.isobox(9, 8.5, 4, 4, 2.5, "blue", 7, 6, 4)
    I.isobox(9, 5.8, 4.6, 4.6, 0.8, "red", 6, 5, 3)
    I.rect(8.5, 1, 1, 2, "grey", 3)


@icon("tr_line", "transit", "Transit line")
def _(I):
    I.line(2.5, 11.5, 8, 11.5, "blue", 4, 2.8)
    I.line(8, 11.5, 13, 5.5, "blue", 4, 2.8)
    I.line(3, 10.6, 8, 10.6, "blue", 6, 0.7)
    for cx, cy in ((3, 11.5), (8, 11.5), (13, 5.5)):
        I.ellipse(cx, cy, 2, 2, "grey", 1)
        I.ellipse(cx, cy, 1.4, 1.4, "#ffffff")


def _track_q(I, x0, y0, x1, y1, c, s):
    I.isoquad(8, 15, [(x0, y0, 1.5), (x1, y0, 1.5), (x1, y1, 1.5), (x0, y1, 1.5)], c, s)


@icon("tr_rail", "transit", "Rail")
def _(I):
    grass_tile(I)
    _track_q(I, 0, -1.2, -7, -5.8, "sand", 4)
    for xx in (-0.3, -1.9, -3.5, -5.1):
        _track_q(I, xx, -1.2, xx - 0.9, -5.8, "brown", 2)
    _track_q(I, 0, -2.1, -7, -2.8, "grey", 7)
    _track_q(I, 0, -4.2, -7, -4.9, "grey", 7)


@icon("tr_tramtrack", "transit", "Tram track")
def _(I):
    grass_tile(I)
    _track_q(I, 0, -1.2, -7, -5.8, "grey", 3)
    _track_q(I, 0, -2.1, -7, -2.8, "grey", 7)
    _track_q(I, 0, -4.2, -7, -4.9, "grey", 7)
    _track_q(I, 0, -3.15, -7, -3.85, "yellow", 5)


@icon("tr_stop_remove", "transit", "Remove stop")
def _(I):
    I.rect(5, 4, 1, 9.5, "grey", 4)
    I.rect(2.5, 2, 6, 4.5, "blue", 5)
    I.rect(2.5, 2, 6, 0.8, "blue", 7)
    I.rect(3.5, 3.6, 4, 1, "#ffffff")
    I.line(8.5, 7.5, 14, 13, "red", 4, 2.4)
    I.line(14, 7.5, 8.5, 13, "red", 4, 2.4)
    I.line(8.8, 7.8, 13.7, 12.7, "red", 6, 1.3)
    I.line(13.7, 7.8, 8.8, 12.7, "red", 6, 1.3)


# ---- industry hubs (iso mini scenes on tiles) ---------------------------------------------------------
def _tree(I, cx, by, h=7, w=4, shades=(3, 4, 5)):
    """Conifer: stacked triangles, trunk at the bottom (by = foot y)."""
    I.rect(cx - 0.5, by - 1.5, 1, 1.5, "brown", 3)
    n = len(shades)
    for i, sh in enumerate(shades):
        top = by - h + i * (h - 2.5) / n
        bot = by - 1.2 - (n - 1 - i) * (h - 2.5) / n
        ww = w * (0.55 + 0.45 * (i + 1) / n) / 2
        I.poly([(cx, top), (cx + ww, bot), (cx - ww, bot)], "green", sh)
        I.poly([(cx, top), (cx, bot), (cx - ww, bot)], "green", sh + 1)


@icon("hub_farm", "industry", "Farm")
def _(I):
    grass_tile(I)
    q = lambda x0, y0, x1, y1, c, s: I.isoquad(8, 15, [(x0, y0, 1.5), (x1, y0, 1.5), (x1, y1, 1.5), (x0, y1, 1.5)], c, s)
    q(-0.3, -0.3, -6.7, -3.4, "brown", 4)
    for i, y in enumerate((-0.6, -1.4, -2.2)):
        q(-0.5, y, -6.5, y - 0.6, "yellow", 6 if i % 2 == 0 else 5)
    I.isobox(10.5, 11, 3.5, 3, 3.5, "red", 5, 5, 3)
    I.gable(10.5, 11, 3.5, 3, 3.5, 2.5, "brown", "x", 6, 3, "red", 4)
    I.isoquad(10.5, 11, [(-1, 0, 0), (-2.5, 0, 0), (-2.5, 0, 2.2), (-1, 0, 2.2)], "sand", 6)


@icon("hub_forestry", "industry", "Forestry")
def _(I):
    grass_tile(I)
    _tree(I, 5, 11, 8, 4.4)
    _tree(I, 10.5, 10.5, 8.5, 4.8)
    _tree(I, 8, 13, 7, 4)
    # log pile
    for cx, cy in ((11, 13), (13, 13)):
        I.ellipse(cx, cy, 1.5, 1.2, "brown", 4)
    I.ellipse(12, 11.8, 1.5, 1.2, "brown", 4)
    I.ellipse(10.7, 13, 0.8, 0.9, "sand", 6)
    I.ellipse(12.7, 13, 0.8, 0.9, "sand", 6)
    I.ellipse(11.7, 11.8, 0.8, 0.9, "sand", 6)


@icon("hub_quarry", "industry", "Quarry")
def _(I):
    I.isotile(8, 15, 7, 7, "grey", 6, 1.5, "brown")
    t = lambda x0, y0, x1, y1, c, s: I.isoquad(8, 15, [(x0, y0, 1.5), (x1, y0, 1.5), (x1, y1, 1.5), (x0, y1, 1.5)], c, s)
    t(-1, -1, -6, -6, "grey", 4)
    t(-2, -2, -5, -5, "grey", 3)
    t(-3, -3, -4.2, -4.2, "grey", 1)
    I.ellipse(11.8, 12.2, 1.6, 1.4, "grey", 7)
    I.ellipse(11.3, 11.8, 0.6, 0.5, "#ffffff")
    I.ellipse(4, 11.6, 1.2, 1, "grey", 7)
    I.ellipse(13.2, 10.3, 1.1, 0.9, "grey", 6)


@icon("hub_mine", "industry", "Mine")
def _(I):
    grass_tile(I)
    I.rect(5, 10.2, 3.2, 2.4, "grey", 1)
    I.line(3.5, 12.5, 6.5, 3.8, "brown", 4, 1.2)
    I.line(9.5, 12.5, 6.5, 3.8, "brown", 3, 1.2)
    I.line(4.6, 9.3, 8.4, 9.3, "brown", 5, 0.9)
    I.ring(6.5, 3.8, 1.5, 1, "grey", 5)
    I.ellipse(11.2, 10.7, 2.3, 1.2, "orange", 5)
    I.ellipse(10.7, 10.3, 1, 0.5, "orange", 7)
    I.rect(9, 11, 4.4, 1.8, "grey", 4)
    I.rect(9, 11, 4.4, 0.6, "grey", 6)
    I.ellipse(10, 13.2, 0.8, 0.8, "grey", 1)
    I.ellipse(12.4, 13.2, 0.8, 0.8, "grey", 1)


@icon("hub_oil", "industry", "Oil")
def _(I):
    grass_tile(I)
    I.ellipse(10.5, 13, 3.2, 1.3, "grey", 1)
    I.ellipse(10, 12.7, 1.5, 0.5, "grey", 3)
    I.rect(4, 11, 7, 1.6, "grey", 4)
    I.poly([(5.8, 11), (8, 5), (10.2, 11)], "orange", 4)
    I.poly([(7, 11), (8, 8), (9, 11)], "grey", 1)
    I.line(3, 7, 12.5, 4.5, "orange", 5, 1.5)
    I.poly([(11.5, 3.6), (13.8, 4.3), (13.3, 7.8), (11.8, 7.8)], "orange", 4)
    I.ellipse(3.8, 7.6, 1.8, 1.8, "grey", 3)
    I.ellipse(3.4, 7.2, 0.7, 0.7, "grey", 6)
    I.ellipse(8, 5, 0.9, 0.9, "grey", 7)


@icon("hub_fish", "industry", "Fishing")
def _(I):
    I.isotile(8, 15, 7, 7, "blue", 5, 1.5, "brown")
    I.line(2.5, 12.8, 5.5, 12.8, "blue", 7, 0.7)
    I.line(10.5, 14, 13.5, 14, "blue", 7, 0.7)
    I.poly([(2, 8.5), (10.5, 8.5), (8.8, 12), (3.8, 12)], "red", 4)
    I.rect(2, 8.5, 8.5, 1, "red", 6)
    I.rect(4, 5.5, 3.5, 3, "sand", 7)
    I.rect(4.8, 6.2, 1.8, 1, "blue", 6)
    I.rect(8.2, 3, 0.8, 5.5, "brown", 3)
    I.poly([(9, 3.2), (11.5, 6), (9, 6)], "yellow", 6)
    I.ellipse(12.3, 11, 2, 1.2, "orange", 5)
    I.poly([(14, 11), (15, 9.8), (15, 12.2)], "orange", 4)
    I.rect(11.2, 10.4, 0.7, 0.7, "grey", 1)
