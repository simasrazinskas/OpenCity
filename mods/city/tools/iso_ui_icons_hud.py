"""HUD icons (time, stats, weather, seasons) for the RCT2-style OpenCity icon set.
Flat front views, chunky shapes, readable at 16 px and 24 px."""

from iso_ui_icon_dsl import icon
from iso_ui_icons_core import person, drop

SKIN = "#f0b888"
WHITE = "#ffffff"


# ---- shared HUD motifs --------------------------------------------------------------------------------
def coin(I, cx, cy, r):
    I.ellipse(cx, cy, r, r, "yellow", 3)
    I.ellipse(cx - 0.3, cy - 0.3, r - 1, r - 1, "yellow", 5)
    I.ellipse(cx - 0.8, cy - 0.9, max(r - 2.2, 0.8), max(r - 2.4, 0.8), "yellow", 6)


def coin_stack(I, cx, by, rx=4.5, n=3):
    """Side view of stacked coins; by = bottom y of lowest coin."""
    for i in range(n):
        y = by - 1.5 - i * 2
        I.ellipse(cx, y + 0.8, rx, 2, "yellow", 3)
        I.ellipse(cx, y, rx, 2, "yellow", 5)
        I.ellipse(cx - 1, y - 0.3, rx - 2, 0.9, "yellow", 6)


def arrow(I, cx, y0, h, up, ramp):
    """Chunky vertical arrow, y0 = top."""
    hw = 3.2
    if up:
        I.rect(cx - 1.1, y0 + h * 0.45, 2.2, h * 0.55, ramp, 4)
        I.poly([(cx - hw, y0 + h * 0.5), (cx, y0), (cx + hw, y0 + h * 0.5)], ramp, 5)
        I.poly([(cx - hw + 0.8, y0 + h * 0.5 - 0.6), (cx, y0 + 0.8), (cx, y0 + h * 0.5 - 0.6)], ramp, 6)
    else:
        I.rect(cx - 1.1, y0, 2.2, h * 0.55, ramp, 4)
        I.poly([(cx - hw, y0 + h * 0.5), (cx, y0 + h), (cx + hw, y0 + h * 0.5)], ramp, 5)
        I.poly([(cx - hw + 0.8, y0 + h * 0.5 + 0.2), (cx, y0 + h - 0.8), (cx, y0 + h * 0.5 + 0.2)], ramp, 6)


def face(I, mood):
    base = {"happy": "yellow", "neutral": "yellow", "sad": "orange"}[mood]
    I.ellipse(8, 8, 6.7, 6.7, base, 3)
    I.ellipse(7.7, 7.7, 5.7, 5.7, base, 5)
    I.ellipse(6.8, 6.6, 3.8, 3.2, base, 6)
    ink = "brown"
    I.rect(5, 5.2, 1.6, 2.2, ink, 0)
    I.rect(9.4, 5.2, 1.6, 2.2, ink, 0)
    if mood == "happy":
        I.poly([(4.5, 9), (11.5, 9), (10.3, 11.6), (5.7, 11.6)], ink, 0)
        I.rect(5.9, 9.8, 4.2, 1, WHITE)
    elif mood == "neutral":
        I.rect(5, 10, 6, 1.3, ink, 0)
    else:
        I.line(4.9, 12.2, 8, 10.3, ink, 0, 1.2)
        I.line(11.1, 12.2, 8, 10.3, ink, 0, 1.2)
        I.rect(3.6, 8.4, 1.1, 1.8, "blue", 6)


# ---- time ---------------------------------------------------------------------------------------------
@icon("time_clock", "time", "Time")
def _(I):
    I.ellipse(8, 8, 6.8, 6.8, "grey", 3)
    I.ellipse(7.8, 7.8, 5.6, 5.6, WHITE)
    I.ellipse(7.2, 7.2, 4.2, 4.2, "grey", 7)
    for x, y in ((7.5, 3), (7.5, 11.8), (3, 7.5), (12, 7.5)):
        I.rect(x, y, 1, 1, "grey", 3)
    I.line(8, 8, 8, 4.6, "red", 3, 1.3)
    I.line(8, 8, 10.8, 9.6, "grey", 1, 1.3)
    I.ellipse(8, 8, 1, 1, "red", 3)


@icon("time_calendar", "time", "Date")
def _(I):
    I.rect(2, 3, 12, 11.5, "grey", 3)
    I.rect(2, 3, 11.5, 11, WHITE)
    I.rect(2, 3, 12, 4, "red", 4)
    I.rect(2, 3, 12, 1.5, "red", 5)
    I.rect(4, 1.2, 1.6, 3, "grey", 2)
    I.rect(10.4, 1.2, 1.6, 3, "grey", 2)
    for r in range(2):
        for c in range(3):
            I.rect(3.6 + c * 3.6, 8.2 + r * 3, 2, 1.8, "blue" if (r, c) != (1, 1) else "red", 4)


# ---- money --------------------------------------------------------------------------------------------
@icon("stat_money", "stats", "Money")
def _(I):
    coin_stack(I, 5.5, 14.5, 4.5, 3)
    coin(I, 10.5, 7.5, 4.8)
    I.text(8.8, 4.6, "$", "yellow", 2)


@icon("stat_income", "stats", "Income")
def _(I):
    coin_stack(I, 5, 14.5, 4, 3)
    arrow(I, 11.5, 1.5, 11, True, "green")


@icon("stat_expenses", "stats", "Expenses")
def _(I):
    coin_stack(I, 5, 14.5, 4, 3)
    arrow(I, 11.5, 3.5, 11, False, "red")


@icon("stat_balance_up", "stats", "Surplus")
def _(I):
    I.poly([(8, 2.5), (14, 13), (2, 13)], "green", 3)
    I.poly([(8, 4.5), (12, 11.5), (4, 11.5)], "green", 5)
    I.poly([(8, 4.5), (8, 11.5), (4, 11.5)], "green", 6)


@icon("stat_balance_down", "stats", "Deficit")
def _(I):
    I.poly([(2, 3), (14, 3), (8, 13.5)], "red", 3)
    I.poly([(4, 4.5), (12, 4.5), (8, 11.5)], "red", 5)
    I.poly([(4, 4.5), (8, 4.5), (8, 11.5)], "red", 6)


# ---- people and mood ----------------------------------------------------------------------------------
def bust(I, cx, top, body="blue", s=1.0):
    """Head + shoulders, front view. top = head top y; total height about 10.5 * s."""
    hr = 2.3 * s
    I.poly([(cx - 3.8 * s, top + 10.5 * s), (cx - 3.5 * s, top + 7.4 * s), (cx - 1.8 * s, top + 5.2 * s),
            (cx + 1.8 * s, top + 5.2 * s), (cx + 3.5 * s, top + 7.4 * s), (cx + 3.8 * s, top + 10.5 * s)], body, 4)
    I.poly([(cx - 3.5 * s, top + 7.4 * s), (cx - 1.8 * s, top + 5.2 * s), (cx, top + 5.2 * s),
            (cx, top + 10.5 * s), (cx - 3.8 * s, top + 10.5 * s)], body, 5)
    I.rect(cx - 0.9 * s, top + 4 * s, 1.8 * s, 1.6 * s, SKIN, 5)
    I.ellipse(cx, top + hr, hr, hr, SKIN, 5)
    I.ellipse(cx - 0.6 * s, top + hr - 0.6 * s, hr * 0.5, hr * 0.45, SKIN, 7)
    I.ellipse(cx, top + hr * 0.45, hr * 0.95, hr * 0.5, "brown", 2)


def star(I, cx, cy, R, r, ramp, s):
    import math
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rr = R if i % 2 == 0 else r
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    I.poly(pts, ramp, s)


@icon("stat_population", "stats", "Population")
def _(I):
    bust(I, 10.8, 2.2, "red", 0.95)
    bust(I, 5.8, 4.2, "blue", 1.0)


@icon("stat_citizen", "stats", "Citizen")
def _(I):
    bust(I, 8, 1.8, "blue", 1.2)


@icon("stat_happiness", "stats", "Happiness")
def _(I):
    face(I, "happy")


@icon("stat_neutral", "stats", "Neutral")
def _(I):
    face(I, "neutral")


@icon("stat_unhappy", "stats", "Unhappy")
def _(I):
    face(I, "sad")


# ---- city indicators ----------------------------------------------------------------------------------
@icon("stat_demand", "stats", "Demand")
def _(I):
    for x, h, ramp in ((1.5, 8, "green"), (5, 5.5, "blue"), (8.5, 11, "yellow"), (12, 3.5, "purple")):
        top = 14 - h
        I.rect(x, top, 2.8, h, ramp, 4)
        I.rect(x, top, 1.1, h, ramp, 6)
        I.rect(x + 2.0, top, 0.8, h, ramp, 3)


@icon("stat_traffic", "stats", "Traffic")
def _(I):
    I.rect(1.5, 8, 13, 4, "red", 4)
    I.rect(1.5, 8, 13, 1.2, "red", 6)
    I.poly([(4, 8), (5.8, 4.2), (10.4, 4.2), (12.4, 8)], "red", 5)
    I.poly([(5.2, 7.6), (6.4, 5.2), (8.1, 5.2), (8.1, 7.6)], "blue", 6)
    I.poly([(9, 7.6), (9, 5.2), (10, 5.2), (11.3, 7.6)], "blue", 5)
    I.rect(13, 8.6, 1.5, 1.2, "yellow", 7)
    for x in (4.8, 11.2):
        I.ellipse(x, 12.2, 2.2, 2.2, "grey", 1)
        I.ellipse(x, 12.2, 0.9, 0.9, "grey", 6)


@icon("stat_vehicles", "stats", "Vehicles")
def _(I):
    # bus (behind, blue/teal) and car (front, red)
    I.rect(1.5, 4, 13, 6.5, "teal", 4)
    I.rect(1.5, 4, 13, 1.2, "teal", 6)
    for x in (2.6, 5.6, 8.6, 11.6):
        I.rect(x, 5.6, 2.2, 2, "blue", 6)
    I.rect(1.5, 9, 13, 1.1, "yellow", 5)
    I.ellipse(4, 11, 1.9, 1.9, "grey", 1)
    I.ellipse(4, 11, 0.7, 0.7, "grey", 6)
    I.ellipse(12, 11, 1.9, 1.9, "grey", 1)
    I.ellipse(12, 11, 0.7, 0.7, "grey", 6)
    I.rect(6.5, 11, 8.5, 3.2, "red", 4)
    I.rect(6.5, 11, 8.5, 0.9, "red", 6)
    I.poly([(8, 11), (9.2, 9), (12.4, 9), (13.4, 11)], "red", 5)
    I.ellipse(8.6, 14, 1.4, 1.4, "grey", 1)
    I.ellipse(13, 14, 1.4, 1.4, "grey", 1)


@icon("stat_pollution", "stats", "Pollution")
def _(I):
    cloud(I, 0, 1.2, 1.0, 5, 4, 3)
    I.ellipse(4.2, 3.6, 1.7, 1.4, "grey", 4)
    I.ellipse(7.4, 2.4, 1.2, 1, "grey", 4)
    I.ellipse(11.4, 3, 1.1, 0.9, "grey", 4)


@icon("stat_tax", "stats", "Tax")
def _(I):
    coin(I, 8, 8, 6.8)
    I.ring(5.8, 5.6, 1.8, 1.2, "brown", 1)
    I.ring(10.2, 10.4, 1.8, 1.2, "brown", 1)
    I.line(10.6, 4.8, 5.4, 11.2, "brown", 1, 1.3)


@icon("stat_xp", "stats", "Experience")
def _(I):
    star(I, 8, 8.6, 7, 3.2, "blue", 3)
    star(I, 7.8, 8.4, 5.6, 2.6, "blue", 5)
    star(I, 7.2, 7.6, 3.4, 1.6, "blue", 6)
    I.pixel(2.5, 2, WHITE)
    I.pixel(13, 3, WHITE)


@icon("stat_dev_point", "stats", "Development point")
def _(I):
    I.ellipse(8, 6.3, 5, 5, "yellow", 4)
    I.poly([(4.5, 8), (11.5, 8), (10, 11), (6, 11)], "yellow", 4)
    I.ellipse(6.6, 4.8, 2.6, 2.6, "yellow", 6)
    I.ellipse(6.2, 4.4, 1.2, 1.2, "yellow", 7)
    I.rect(5.8, 11, 4.4, 1.5, "grey", 5)
    I.rect(6, 12.5, 4, 1.5, "grey", 3)
    I.rect(7, 14, 2, 0.8, "grey", 2)
    I.line(6.5, 8, 8, 6.5, "orange", 4, 0.8)
    I.line(9.5, 8, 8, 6.5, "orange", 4, 0.8)


@icon("stat_permit", "stats", "Permit")
def _(I):
    I.rect(2.5, 1.5, 10, 13, "grey", 3)
    I.rect(2.5, 1.5, 9.5, 12.5, WHITE)
    I.rect(4, 3.5, 6, 1, "grey", 4)
    I.rect(4, 6, 6, 1, "grey", 4)
    I.rect(4, 8.5, 3.5, 1, "grey", 4)
    I.ring(10.4, 11, 3.3, 1.4, "red", 4)
    I.ellipse(10.4, 11, 1.6, 1.6, "red", 5)
    I.rect(9.8, 4.5, 1, 0, "red", 4)


@icon("stat_loan", "stats", "Loan")
def _(I):
    I.poly([(8, 1.8), (14.5, 5.8), (1.5, 5.8)], "sand", 6)
    I.poly([(8, 1.8), (14.5, 5.8), (8, 5.8)], "sand", 4)
    I.ellipse(8, 4.4, 1.1, 1.1, "yellow", 5)
    for x in (2.8, 5.8, 8.8, 11.8):
        I.rect(x, 6.8, 2, 5, "sand", 7)
        I.rect(x + 1.3, 6.8, 0.7, 5, "sand", 4)
    I.rect(1.5, 11.8, 13, 1.4, "sand", 5)
    I.rect(1, 13.2, 14, 1.8, "sand", 3)


@icon("stat_fee", "stats", "Fee")
def _(I):
    I.poly([(1.5, 8.5), (5.5, 3.5), (14.5, 3.5), (14.5, 13.5), (5.5, 13.5)], "orange", 5)
    I.poly([(1.5, 8.5), (5.5, 3.5), (14.5, 3.5), (14.5, 5), (6, 5), (3, 8.5)], "orange", 7)
    I.rect(13.3, 5, 1.2, 8.5, "orange", 3)
    I.ellipse(5.3, 8.5, 1.3, 1.3, "brown", 1)
    I.text(8.4, 5.4, "$", "orange", 1)


# ---- places, work, services ---------------------------------------------------------------------------
def house(I, x0=2, y0=2, w=12, wall="sand", roof="red"):
    cx = x0 + w / 2.0
    I.rect(x0 + 1, y0 + 5.2, w - 2, 7.2, wall, 5)
    I.rect(x0 + 1, y0 + 5.2, 1.2, 7.2, wall, 6)
    I.rect(x0 + w - 2.2, y0 + 5.2, 1.2, 7.2, wall, 3)
    I.poly([(x0 - 0.5, y0 + 5.8), (cx, y0), (x0 + w + 0.5, y0 + 5.8)], roof, 5)
    I.poly([(x0 - 0.5, y0 + 5.8), (cx, y0), (cx, y0 + 5.8)], roof, 6)
    I.rect(x0 + w - 3.6, y0 + 0.8, 1.6, 2.6, "red", 3)


@icon("stat_home", "stats", "Homes")
def _(I):
    house(I)
    I.rect(6.8, 10, 2.6, 4.4, "brown", 3)
    I.rect(3.6, 8.8, 2.2, 2.2, "blue", 6)
    I.rect(10.2, 8.8, 2.2, 2.2, "blue", 5)


@icon("stat_households", "stats", "Households")
def _(I):
    house(I, 1, 1.5, 11.5)
    I.rect(3, 8.6, 2, 2, "blue", 6)
    I.rect(7, 10, 2.4, 3, "brown", 3)
    bust(I, 12.2, 7, "green", 0.55)


@icon("stat_work", "stats", "Workplaces")
def _(I):
    I.rect(5.3, 2.5, 5.4, 4, "brown", 2)
    I.rect(6.6, 3.8, 2.8, 3, None)
    I.rect(1.5, 5.5, 13, 8.5, "brown", 4)
    I.rect(1.5, 5.5, 13, 1.6, "brown", 6)
    I.rect(1.5, 8.6, 13, 0.9, "brown", 2)
    I.rect(1.5, 5.5, 1.2, 8.5, "brown", 5)
    I.rect(6.8, 8, 2.4, 2.4, "yellow", 5)
    I.rect(6.8, 8, 1, 1, "yellow", 7)


@icon("stat_student", "stats", "Students")
def _(I):
    I.poly([(4, 8.3), (4, 12), (8, 13.8), (12, 12), (12, 8.3), (8, 10.2)], "purple", 3)
    I.poly([(4, 8.3), (4, 12), (8, 13.8), (8, 10.2)], "purple", 4)
    I.poly([(8, 2.5), (15, 6.5), (8, 10.5), (1, 6.5)], "purple", 5)
    I.poly([(8, 2.5), (1, 6.5), (8, 7.5)], "purple", 7)
    I.line(13.2, 7.2, 13.2, 12, "yellow", 5, 1)
    I.rect(12.6, 11.6, 1.2, 2, "yellow", 4)


@icon("stat_tourist", "stats", "Tourists")
def _(I):
    I.rect(5, 3, 5.5, 3, "grey", 4)
    I.rect(1.5, 5, 13, 9, "red", 4)
    I.rect(1.5, 5, 13, 1.6, "red", 6)
    I.rect(1.5, 11.6, 13, 2.4, "red", 3)
    I.ellipse(8, 9.6, 3.6, 3.6, "grey", 2)
    I.ellipse(8, 9.6, 2.6, 2.6, "blue", 4)
    I.ellipse(7.3, 8.9, 1.1, 1.1, "blue", 7)
    I.rect(11.6, 6.8, 2, 1.2, "yellow", 7)


@icon("stat_hotel", "stats", "Hotel")
def _(I):
    I.rect(1.5, 3, 2.2, 11, "brown", 3)
    I.rect(1.5, 3, 1, 11, "brown", 5)
    I.rect(12.8, 9, 1.7, 5, "brown", 3)
    I.rect(3.7, 8.8, 9.3, 3.2, "blue", 4)
    I.rect(3.7, 8.8, 9.3, 1, "blue", 6)
    I.rect(3.7, 11.2, 10.8, 1.6, "brown", 4)
    I.ellipse(5.8, 7.6, 2.2, 1.4, WHITE)
    I.rect(1.5, 12.8, 2.2, 1.2, "brown", 2)
    I.rect(5, 1.8, 0, 0, "brown", 2)


@icon("stat_jobs", "stats", "Jobs")
def _(I):
    I.ellipse(8, 9.8, 5.8, 6, "orange", 4)
    I.ellipse(7, 8.2, 3.6, 3.8, "orange", 6)
    I.rect(6.8, 3.2, 2.4, 6, "orange", 6)
    I.rect(6.8, 3.2, 0.9, 6, "orange", 7)
    I.rect(0, 12, 16, 4, None)
    I.rect(1, 10.8, 14, 1.3, "orange", 3)
    I.rect(1, 10.8, 14, 0.5, "orange", 5)
    I.rect(1, 12, 14, 1, "orange", 2)


@icon("stat_unemployed", "stats", "Unemployed")
def _(I):
    bust(I, 5.5, 3.6, "grey", 0.95)
    I.text(10, 3.8, "?", "red", 5)


@icon("stat_health", "stats", "Health")
def _(I):
    I.ellipse(5.3, 5.6, 3.8, 3.8, "red", 4)
    I.ellipse(10.7, 5.6, 3.8, 3.8, "red", 4)
    I.poly([(1.6, 6.6), (14.4, 6.6), (8, 14.6)], "red", 4)
    I.ellipse(4.6, 4.6, 1.8, 1.4, "red", 6)
    I.poly([(1.8, 6.4), (8, 6.4), (8, 14.6)], "red", 5)
    I.ellipse(5.3, 5.2, 3, 2.8, "red", 5)
    I.ellipse(4.5, 4.4, 1.5, 1.2, "red", 7)
    I.rect(6.9, 4.8, 2.2, 6, WHITE)
    I.rect(5, 6.7, 6, 2.2, WHITE)


@icon("stat_crime", "stats", "Crime")
def _(I):
    I.ellipse(8, 8, 2.1, 1.5, "grey", 6)
    I.ellipse(8, 8, 1.0, 0.6, None)
    I.ring(4.6, 11, 3.6, 1.9, "grey", 5)
    I.ring(11.4, 5, 3.6, 1.9, "grey", 5)
    I.rect(5.6, 6.4, 1.8, 1.6, "blue", 4)
    I.rect(8.8, 8.2, 1.8, 1.6, "blue", 4)
    I.rect(2.2, 9.6, 0.9, 2.6, "grey", 7)


@icon("stat_land_value", "stats", "Land value")
def _(I):
    I.isotile(8, 15, 6, 6, "green", 5, 1.5, "brown")
    coin(I, 8, 5.8, 4.7)
    I.text(6.3, 2.4, "$", "yellow", 1)


@icon("stat_temperature", "stats", "Temperature")
def _(I):
    I.rect(6, 1.5, 4, 10, "grey", 3)
    I.rect(6.6, 1.8, 2.8, 9.4, WHITE)
    I.rect(7.2, 5, 1.6, 7, "red", 4)
    I.ellipse(8, 12.1, 3.5, 3.4, "grey", 3)
    I.ellipse(8, 12.1, 2.6, 2.5, "red", 4)
    I.ellipse(7.3, 11.4, 0.9, 0.9, "red", 7)
    for y in (3, 5.5, 8):
        I.rect(11, y, 2.5, 0.9, "grey", 4)


@icon("stat_buildings", "stats", "Buildings")
def _(I):
    I.rect(1.5, 7, 4.5, 7.5, "grey", 5)
    I.rect(5.5, 2.5, 5, 12, "blue", 4)
    I.rect(10, 6, 4.5, 8.5, "sand", 5)
    I.rect(5.5, 2.5, 1.3, 12, "blue", 6)
    I.rect(1.5, 7, 1, 7.5, "grey", 7)
    I.rect(10, 6, 1, 8.5, "sand", 7)
    I.rect(9.2, 2.5, 1.3, 12, "blue", 3)
    for y in (4.5, 7.5, 10.5):
        I.rect(7.4, y, 1.2, 1.4, "yellow", 6)
    for y in (9, 12):
        I.rect(3.3, y, 1.2, 1.2, "yellow", 6)
        I.rect(12, y - 1.5, 1.2, 1.2, "yellow", 6)


# ---- weather ------------------------------------------------------------------------------------------
def sun(I, cx, cy, r, ray_in=1.5, ray_len=2.0, t=1.4):
    import math
    for i in range(8):
        a = i * math.pi / 4
        d0, d1 = r + ray_in, r + ray_in + ray_len
        I.line(cx + d0 * math.cos(a), cy + d0 * math.sin(a), cx + d1 * math.cos(a), cy + d1 * math.sin(a),
               "orange", 5, t)
    I.ellipse(cx, cy, r, r, "yellow", 4)
    I.ellipse(cx - 0.3, cy - 0.3, r - 0.8, r - 0.8, "yellow", 5)
    I.ellipse(cx - 0.9, cy - 0.9, r * 0.5, r * 0.5, "yellow", 7)


def cloud(I, ox=0.0, oy=0.0, sc=1.0, hi=7, mid=6, lo=5, ramp="grey"):
    """Fluffy cloud in the box x 1..15, y 4..13.5 (before transform)."""
    def parts(dy, s):
        I.ellipse(ox + 4.8 * sc, oy + (10 + dy) * sc, 3.3 * sc, 3 * sc, ramp, s)
        I.ellipse(ox + 8.2 * sc, oy + (7.2 + dy) * sc, 4 * sc, 3.8 * sc, ramp, s)
        I.ellipse(ox + 12 * sc, oy + (10 + dy) * sc, 3 * sc, 2.8 * sc, ramp, s)
        I.rect(ox + 4.8 * sc, oy + (10 + dy) * sc, 7.2 * sc, 3.2 * sc, ramp, s)
    parts(0.5, lo)
    parts(-0.3, mid)
    hc = WHITE if hi >= 7 else ramp
    I.ellipse(ox + 7.2 * sc, oy + 5.8 * sc, 2.4 * sc, 1.8 * sc, hc, hi)
    I.ellipse(ox + 3.8 * sc, oy + 9 * sc, 1.4 * sc, 1.1 * sc, hc, hi)


def snowflake(I, cx, cy, R, c, s, t=1.3):
    import math
    for i in range(3):
        a = i * math.pi / 3
        dx, dy = R * math.cos(a), R * math.sin(a)
        I.line(cx - dx, cy - dy, cx + dx, cy + dy, c, s, t)
    for i in range(6):
        a = i * math.pi / 3
        bx, by = cx + 0.62 * R * math.cos(a), cy + 0.62 * R * math.sin(a)
        for da in (-0.9, 0.9):
            I.line(bx, by, bx + 0.38 * R * math.cos(a + da), by + 0.38 * R * math.sin(a + da), c, s, t * 0.8)


@icon("wx_sun", "weather", "Sunny")
def _(I):
    sun(I, 8, 8, 4.2, 1.2, 2.0)


@icon("wx_partly", "weather", "Partly cloudy")
def _(I):
    sun(I, 5.8, 5.6, 3.1, 1.0, 1.5, 1.3)
    cloud(I, 2.2, 3.4, 0.85)


@icon("wx_cloud", "weather", "Cloudy")
def _(I):
    cloud(I, 0, 0.5, 1.0, 7, 7, 5)


@icon("wx_rain", "weather", "Rain")
def _(I):
    cloud(I, 0, -2.2, 0.95, 6, 5, 3)
    for x in (4.2, 7.8, 11.4):
        I.line(x + 0.8, 10.6, x - 0.4, 14.2, "blue", 5, 1.4)
        I.line(x + 0.8, 10.6, x + 0.5, 11.6, "blue", 7, 1.2)


@icon("wx_snow", "weather", "Snow")
def _(I):
    cloud(I, 0, -2.2, 0.95, 7, 7, 5)
    for x, y in ((4, 11.6), (8, 12.8), (12, 11.6), (6, 14.2), (10, 14.4)):
        I.ellipse(x, y, 0.95, 0.95, WHITE)


@icon("wx_storm", "weather", "Storm")
def _(I):
    cloud(I, 0, -2.4, 0.95, 5, 4, 3)
    I.poly([(10.8, 7.4), (5.4, 12), (8.2, 12), (6, 15), (12.8, 9.6), (9.8, 9.6), (12.2, 7.4)], "yellow", 6)
    I.poly([(10.8, 7.4), (5.4, 12), (7.2, 12), (9.4, 8.6)], "yellow", 7)


@icon("wx_fog", "weather", "Fog")
def _(I):
    cloud(I, 0.6, -2.4, 0.85, 7, 6, 5)
    I.rect(2, 9.8, 12, 1.4, "grey", 7)
    I.rect(4, 12, 11, 1.4, "grey", 6)
    I.rect(1.5, 14.2, 9, 1, "grey", 5)


@icon("wx_night", "weather", "Clear night")
def _(I):
    I.ellipse(7.3, 8.6, 5.7, 5.7, "yellow", 4)
    I.ellipse(6.8, 8.1, 4.8, 4.8, "yellow", 6)
    I.ellipse(10.6, 6.4, 4.8, 4.8, None)
    for x, y in ((12.4, 11.2), (11.5, 3.6)):
        I.rect(x, y - 1, 1, 3, WHITE)
        I.rect(x - 1, y, 3, 1, WHITE)
    I.pixel(14, 7.5, WHITE)


@icon("wx_heat", "weather", "Heat wave")
def _(I):
    sun(I, 5.6, 6, 3.3, 0.9, 1.5, 1.3)
    I.rect(10.6, 3.5, 3.4, 8.5, "grey", 3)
    I.rect(11.2, 4, 2.2, 8, WHITE)
    I.rect(11.7, 5.6, 1.2, 7, "red", 4)
    I.ellipse(12.3, 12.7, 2.4, 2.3, "grey", 3)
    I.ellipse(12.3, 12.7, 1.7, 1.6, "red", 4)


@icon("wx_cold", "weather", "Cold snap")
def _(I):
    snowflake(I, 8, 8, 6.4, "blue", 6)
    I.ellipse(8, 8, 1.5, 1.5, WHITE)


# ---- seasons ------------------------------------------------------------------------------------------
@icon("season_spring", "seasons", "Spring")
def _(I):
    import math
    I.rect(7.4, 10, 1.3, 5, "green", 4)
    I.poly([(8.6, 13.2), (13, 10.8), (12, 14.4)], "green", 5)
    I.poly([(8.6, 13.2), (13, 10.8), (10.5, 12.8)], "green", 6)
    for i in range(5):
        a = -math.pi / 2 + i * 2 * math.pi / 5
        px, py = 8 + 3.3 * math.cos(a), 6.2 + 3.3 * math.sin(a)
        I.ellipse(px, py, 2.4, 2.4, "red", 6)
        I.ellipse(px - 0.4, py - 0.4, 1.4, 1.4, "red", 7)
    I.ellipse(8, 6.2, 1.9, 1.9, "yellow", 5)
    I.ellipse(7.6, 5.8, 0.8, 0.8, "yellow", 7)


@icon("season_summer", "seasons", "Summer")
def _(I):
    sun(I, 8, 6.2, 3.6, 1.1, 1.8)
    I.poly([(1.5, 14.5), (3.5, 10.8), (7, 12), (8, 14.5)], "green", 5)
    I.poly([(1.5, 14.5), (3.5, 10.8), (5, 13)], "green", 6)
    I.poly([(8, 14.5), (9.5, 11), (13, 10.6), (14.5, 14.5)], "green", 4)
    I.poly([(8, 14.5), (9.5, 11), (11, 13.6)], "green", 5)


@icon("season_autumn", "seasons", "Autumn")
def _(I):
    pts = [(8, 1.5), (9.6, 4.4), (12.2, 3.4), (11.6, 6.6), (14.5, 7), (12.2, 9.2), (13.2, 11.6), (9.2, 11),
           (8.6, 13.4), (7.4, 13.4), (6.8, 11), (2.8, 11.6), (3.8, 9.2), (1.5, 7), (4.4, 6.6), (3.8, 3.4),
           (6.4, 4.4)]
    I.poly(pts, "orange", 5)
    I.poly([(8, 1.5), (6.4, 4.4), (3.8, 3.4), (4.4, 6.6), (1.5, 7), (3.8, 9.2), (2.8, 11.6), (6.8, 11), (7.4, 13.4),
            (8, 13.4)], "orange", 6)
    I.poly([(8, 1.5), (9.6, 4.4), (12.2, 3.4), (11.6, 6.6), (14.5, 7), (12.2, 9.2), (13.2, 11.6), (9.2, 11),
            (8.6, 13.4), (8, 13.4)], "red", 4)
    I.rect(7.5, 5, 1, 9.5, "orange", 2)
    I.line(8, 9, 11.2, 6.4, "orange", 2, 0.8)
    I.line(8, 9, 4.8, 6.4, "orange", 3, 0.8)


@icon("season_winter", "seasons", "Winter")
def _(I):
    I.rect(7, 12.5, 2.2, 2.5, "brown", 3)
    for top, bot, hw in ((1.5, 6, 3.6), (4, 9.5, 4.8), (7, 12.8, 6.2)):
        I.poly([(8, top), (8 + hw, bot), (8 - hw, bot)], "green", 3)
        I.poly([(8, top), (8, bot), (8 - hw, bot)], "green", 5)
        # snow cap
        k = 0.42
        I.poly([(8, top), (8 + hw * k, top + (bot - top) * k), (8 - hw * k, top + (bot - top) * k)], WHITE)
    I.rect(5, 12, 1.4, 0.9, WHITE)
    I.rect(9.8, 9.2, 1.6, 0.9, WHITE)
    I.pixel(2, 3, WHITE)
    I.pixel(13.5, 2.5, WHITE)
    I.pixel(1.8, 9, WHITE)
    I.pixel(14, 6, WHITE)
