"""32x32 city HUD icons (white + teal glyphs on transparent), drawn by name."""

import math
from uiglyphs import *  # noqa: F401,F403

W, TL = WHITE, TEAL


def i_road(g):
    g.poly([(11.5, 3), (20.5, 3), (29, 29), (3, 29)], W)
    g.poly([(11.5, 3), (13, 3), (6.5, 29), (3, 29)], (204, 160, 108, 255))
    for y0, y1, xa, xb in ((5, 9, 15.2, 16.8), (12, 17, 14.6, 17.4), (20, 26, 14, 18)):
        g.poly([(xa, y0), (xb, y0), (xb + 0.2, y1), (xa - 0.2, y1)], TL)


def i_res_low(g):
    c = ZONE['res-low']
    zone_tile(g, c)
    g.poly([(16, 6.5), (26, 15), (23.5, 15), (23.5, 25), (8.5, 25), (8.5, 15), (6, 15)], c)
    g.rrect(13.5, 17.5, 5, 7.5, 1, fill=SLATE)


def i_res_high(g):
    c = ZONE['res-high']
    zone_tile(g, c)
    g.rrect(8, 6, 16, 19.5, 1.5, fill=c)
    for r in range(4):
        for k in range(2):
            g.rect(11 + k * 6.2, 9 + r * 3.8, 3.4, 2.2, SLATE)
    g.rect(14.2, 22, 3.6, 3.5, SLATE)


def i_com_low(g):
    c = ZONE['com-low']
    zone_tile(g, c)
    g.rect(7, 14.5, 18, 10.5, c)
    g.poly([(6, 8), (26, 8), (28, 14.5), (4, 14.5)], c)
    for i in range(1, 5):
        x0 = 4 + i * 4.8
        g.line(x0 + 0.1 * 2, 8, x0 - 0.4, 14.5, 1.1, SLATE)
    g.rect(10, 17.5, 5.5, 4.5, SLATE)
    g.rect(18, 17.5, 4, 7.5, SLATE)


def i_com_high(g):
    c = ZONE['com-high']
    zone_tile(g, c)
    g.rect(9, 8, 14, 17.5, c)
    g.rect(12, 5.5, 8, 3, c)
    for r in range(4):
        g.rect(11, 10.5 + r * 3.7, 10, 1.7, SLATE)


def i_ind(g):
    c = ZONE['ind']
    zone_tile(g, c)
    g.rect(21, 6.5, 3.5, 11, c)
    g.poly([(7, 25.5), (7, 14), (12.5, 18), (12.5, 14), (18, 18), (18, 14), (24.5, 18), (24.5, 25.5)], c)
    g.rect(10, 20.5, 3, 3, SLATE)
    g.rect(15.5, 20.5, 3, 3, SLATE)


def i_off(g):
    c = ZONE['off']
    zone_tile(g, c)
    g.poly([(9, 25.5), (9, 8.5), (23, 5.5), (23, 25.5)], c)
    for r in range(4):
        for k in range(2):
            g.rect(11.2 + k * 5.6, 9.5 + r * 3.9, 3.4, 2.4, SLATE)


def i_dezone(g):
    g.rrect(2.5, 2.5, 27, 27, 6, fill=(255, 255, 255, 22), stroke=(204, 160, 108, 255), sw=1.5)
    g.line(8, 24, 24, 8, 3, RED)
    g.ring(16, 16, 9, 2.4, RED)


def i_bulldoze(g):
    g.rrect(6, 22, 20, 6.5, 3.2, fill=GREY)
    g.circle(9.5, 25.2, 1.5, SLATE)
    g.circle(22.5, 25.2, 1.5, SLATE)
    g.rrect(9, 13.5, 14, 8.5, 1.5, fill=W)
    g.rrect(16, 7.5, 7.5, 7, 1.5, fill=W)
    g.rect(18, 9.3, 4, 3, TL)
    g.line(10, 17.5, 4.5, 21.5, 2.2, W)
    g.poly([(1.5, 11), (6.2, 11.5), (6, 26.5), (1.5, 25)], TL)


def i_power(g):
    bolt(g, 3.4, 1.0, 1.58, AMBER)


def i_water(g):
    drop(g, 16, 18.5, 8.8, W, TL)


def i_police(g):
    shield(g, 16, 3.2, 21, 25, W)
    star(g, 16, 14.5, 6.2, 2.6, TL)


def i_fire(g):
    flame(g, 16, 25.5, 1.55, W, TL)


def i_health(g):
    g.ring(16, 16, 12.5, 2.2, W)
    cross(g, 16, 16, 7, 4.8, TL)


def i_education(g):
    cap(g, 15, 14, 1.0, W, TL)


def i_parks(g):
    tree(g, 16, 28, 1.5, GREEN, W)


def i_infoviews(g):
    for i, a in enumerate((255, 190, 130)):
        y = 22 - i * 7
        g.poly([(16, y - 6.5), (29, y), (16, y + 6.5), (3, y)], TL if i == 2 else col(W, a))
        if i < 2:
            g.poly([(16, y - 6.5 + 1.6), (24.5, y), (16, y + 6.5 - 1.6), (7.5, y)], (46, 22, 12, 150))


def i_budget(g):
    for i, (h, c) in enumerate(((10, W), (16, W), (22, TL))):
        g.rrect(5 + i * 8.5, 28 - h, 6, h, 1.5, fill=c)
    g.line(4, 8, 11, 6.5, 1.4, (255, 230, 170, 140))


def i_pause(g):
    g.rrect(8, 6, 5.4, 20, 1.6, fill=W)
    g.rrect(18.6, 6, 5.4, 20, 1.6, fill=W)


def i_play(g):
    g.poly([(10, 5.5), (26.5, 16), (10, 26.5)], W)


def i_fast(g):
    g.poly([(4.5, 7), (16, 16), (4.5, 25)], W)
    g.poly([(16, 7), (27.5, 16), (16, 25)], W)


def i_faster(g):
    g.poly([(2.5, 8.5), (11.5, 16), (2.5, 23.5)], W)
    g.poly([(11, 8.5), (20, 16), (11, 23.5)], W)
    g.poly([(19.5, 8.5), (28.5, 16), (19.5, 23.5)], W)


def i_money(g):
    g.circle(16, 16, 13, AMBER)
    g.ring(16, 16, 9.8, 1.4, (150, 98, 10, 255))
    dollar(g, 16, 16, 6.2, (120, 76, 6, 255))


def i_population(g):
    person(g, 21.5, 15, 1.1, GREY)
    person(g, 11.5, 17, 1.3, W)


def i_happiness(g):
    g.circle(16, 16, 13, TL)
    g.circle(11.5, 12.5, 1.9, SLATE)
    g.circle(20.5, 12.5, 1.9, SLATE)
    g.arc(16, 15.5, 7.2, 25, 155, 2.2, SLATE)


def i_close(g):
    g.line(8, 8, 24, 24, 3.2, W)
    g.line(24, 8, 8, 24, 3.2, W)


def i_milestone(g):
    star(g, 16, 16.5, 13.5, 5.6, AMBER)


def i_zoning(g):
    for i, k in enumerate(('res-low', 'com-low', 'ind', 'off')):
        g.rrect(3 + (i % 2) * 13.5, 3 + (i // 2) * 13.5, 12, 12, 3, fill=ZONE[k])


def i_services(g):
    g.poly([(16, 4), (29, 11.5), (3, 11.5)], W)
    for i in range(4):
        g.rect(6.2 + i * 5.8, 14, 3, 9, TL)
    g.rrect(3.5, 25, 25, 3.2, 1, fill=W)


def i_speed1(g):
    i_play(g)


# ---- extras ---------------------------------------------------------------

def i_demand(g):
    g.rrect(4, 4, 24, 24, 4, fill=(255, 255, 255, 24), stroke=GREY_DIM, sw=1.2)
    g.poly([(16, 6), (22.5, 15), (9.5, 15)], GREEN)
    g.poly([(16, 26), (22.5, 17), (9.5, 17)], RED)


def i_info(g):
    g.circle(16, 16, 13, TL)
    g.circle(16, 9.5, 2, SLATE)
    g.rrect(14.1, 13.2, 3.8, 11, 1.4, fill=SLATE)


def i_warning(g):
    g.poly([(16, 3), (30, 27.5), (2, 27.5)], AMBER)
    g.rrect(14.3, 11, 3.4, 9, 1.5, fill=SLATE)
    g.circle(16, 23.4, 2, SLATE)


def i_traffic(g):
    g.rrect(3, 12, 26, 10, 4, fill=W)
    g.poly([(8, 12.5), (11.5, 6.5), (20.5, 6.5), (24, 12.5)], W)
    g.poly([(11, 11.5), (13, 8), (16, 8), (16, 11.5)], SLATE)
    g.poly([(17.5, 11.5), (17.5, 8), (20, 8), (22, 11.5)], SLATE)
    g.circle(9.5, 22.5, 3.6, TL)
    g.circle(22.5, 22.5, 3.6, TL)


def i_pollution(g):
    for cx, cy, r in ((11, 17, 6.5), (20, 15, 8), (24.5, 19.5, 5.5), (16, 21, 6)):
        g.circle(cx, cy, r, GREY)
    g.rect(8, 22, 18, 5, GREY_DIM)


def i_tax(g):
    g.circle(9.5, 9.5, 4.3, W)
    g.circle(22.5, 22.5, 4.3, W)
    g.line(24.5, 6, 7.5, 26, 2.8, TL)


def i_up(g):
    g.poly([(16, 6), (27, 20), (5, 20)], GREEN)


def i_down(g):
    g.poly([(16, 26), (27, 12), (5, 12)], RED)


def i_check(g):
    g.polyline([(6, 17), (13, 24), (26, 8)], 4, TL)


def i_plus(g):
    cross(g, 16, 16, 11, 4.4, W)


def i_minus(g):
    g.rrect(5, 13.8, 22, 4.4, 1, fill=W)


def i_electricity(g):
    i_power(g)


ICONS = [
    ('road', i_road), ('zone-res-low', i_res_low), ('zone-res-high', i_res_high),
    ('zone-com-low', i_com_low), ('zone-com-high', i_com_high), ('zone-ind', i_ind), ('zone-off', i_off),
    ('dezone', i_dezone), ('bulldoze', i_bulldoze), ('power', i_power), ('water', i_water),
    ('police', i_police), ('fire', i_fire), ('health', i_health), ('education', i_education),
    ('parks', i_parks), ('infoviews', i_infoviews), ('budget', i_budget), ('pause', i_pause),
    ('play', i_play), ('fast', i_fast), ('faster', i_faster), ('money', i_money),
    ('population', i_population), ('happiness', i_happiness), ('close', i_close),
    ('milestone', i_milestone), ('zoning', i_zoning), ('services', i_services),
    # extras
    ('demand', i_demand), ('info', i_info), ('warning', i_warning), ('traffic', i_traffic),
    ('pollution', i_pollution), ('tax', i_tax), ('up', i_up), ('down', i_down),
    ('check', i_check), ('plus', i_plus), ('minus', i_minus),
]
# new HUD icons (roads, utilities, zones, industry, transit, panels, misc) live in uicityicons2
from uicityicons2 import ICONS2  # noqa: E402
ICONS += ICONS2

ALIASES = {'speed-1': 'play', 'speed-2': 'fast', 'speed-3': 'faster', 'electricity': 'power'}
