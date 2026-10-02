"""iso_life_mglyphs_b - status glyph art, part B (extras). Registers into the GLYPHS dict of part A."""
from iso_life_mdraw import art, line, px
from iso_life_mglyphs_a import GLYPHS, P, reg, slash


@reg("noeducation")
def _():
    im = art([
        "...dd...",
        "..dddd..",
        ".dddddd.",
        "dddddddd",
        ".dd..dd.",
        ".dd..dd.",
        "y.dddd.y",
        "y......y",
    ], P)
    return slash(im)


@reg("nohealthcare")
def _():
    im = art([
        "..WWWW..",
        "..WrrW..",
        "WWWrrWWW",
        "WrrrrrrW",
        "WrrrrrrW",
        "WWWrrWWW",
        "..WrrW..",
        "..WWWW..",
    ], P)
    return slash(im, "b")


@reg("nopolice")
def _():
    im = art([
        "...yy...",
        "yyyyyyyy",
        ".yyYYyy.",
        ".yyYYyy.",
        ".yyyyyy.",
        "..yyyy..",
        "..yyyy..",
        "...yy...",
    ], P)
    return slash(im, "r")


@reg("nofirecover")
def _():
    im = art([
        "..rrr...",
        ".rrrrrr.",
        "rrrrrrrr",
        "rrWWWWrr",
        "rrWrrWrr",
        "rrWWWWrr",
        ".rrrrrr.",
        "..rrrr..",
    ], P)
    return slash(im, "b")


@reg("nopowerbuild")
def _():
    im = art([
        "..oooo..",
        ".ooOOoo.",
        "oooOOooo",
        "oooooooo",
        "kkkkkkkk",
        "...yy...",
        "..yYy...",
        "..yy....",
    ], P)
    # small bolt on hat
    for (x, y) in [(4, 1), (3, 2), (4, 2), (3, 3), (4, 3)]:
        px(im, x, y, "Y")
    return im


@reg("lowlandvalue")
def _():
    im = art([
        "..rrrr..",
        "..rrrr..",
        "..rrrr..",
        "rrrrrrrr",
        ".rrrrrr.",
        "..rrrr..",
        "llllllll",
        "llllllll",
    ], P)
    return im


@reg("homeless")
def _():
    return art([
        "..ss....",
        ".ssss...",
        "..ss..nn",
        ".nnnnnnn",
        "nNNNNNNn",
        "nNNNNNNn",
        "nNNNNNNn",
        "nnnnnnnn",
    ], P)


@reg("deadbody")
def _():
    return art([
        ".ggggg.",
        "gGGGGGg",
        "gGGkGGg",
        "gGkkkGg",
        "gGGkGGg",
        "gGGkGGg",
        "gGGGGGg",
        "ggggggg",
    ], P)


@reg("nofuel")
def _():
    im = art([
        "..rrr...",
        "..r.rr..",
        ".rrrrrr.",
        ".rWWWWr.",
        ".rWkkWr.",
        ".rWWWWr.",
        ".rrrrrr.",
        ".rrrrrr.",
    ], P)
    return slash(im, "d")


# 24 CityProblem kinds (order from INVENTORY) then extras
PROBLEMS = [
    ("nopower", "No power"), ("nowater", "No water"), ("nosewage", "No sewage"), ("dirtywater", "Dirty water"),
    ("noroad", "No road"), ("abandoned", "Abandoned"), ("unhappy", "Unhappy"), ("noworkers", "No workers"),
    ("nocustomers", "No customers"), ("nogoods", "No goods"), ("garbage", "Garbage"), ("fire", "Fire"),
    ("crime", "Crime"), ("sick", "Sick"), ("ambulance", "Ambulance"), ("highrent", "High rent"),
    ("traffic", "Traffic jam"), ("airpollution", "Air pollution"), ("groundpollution", "Ground poll."),
    ("noise", "Noise"), ("leveledup", "Levelled up"), ("collapsed", "Collapsed"), ("noservice", "No service"),
    ("flooded", "Flooded"),
]
EXTRAS = [
    ("noeducation", "No education"), ("nohealthcare", "No health"), ("nopolice", "No police"),
    ("nofirecover", "No fire cover"), ("nopowerbuild", "No power (build)"), ("lowlandvalue", "Low land value"),
    ("homeless", "Homeless"), ("deadbody", "Awaiting hearse"), ("nofuel", "No fuel"),
]
