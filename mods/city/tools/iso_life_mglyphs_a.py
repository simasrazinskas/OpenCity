"""iso_life_mglyphs_a - status glyph art, part A. Each glyph is <= 8x8 fill art; the bubble builder adds the dark outline."""
import numpy as np

from iso_life_mdraw import art, line, px, rect, canvas

P = {
    "k": "#26262c", "W": "#ffffff", "g": "#7e8086", "G": "#c4c6ca", "d": "#4a4d54",
    "y": "#f0c020", "Y": "#fff070", "o": "#e07418", "O": "#ffa848", "r": "#d02820", "R": "#8a1a1a",
    "b": "#2a62c8", "B": "#7ab0f4", "n": "#a06a38", "N": "#6a4222", "l": "#2f9a3c", "L": "#7ed060",
    "p": "#a050c0", "s": "#f0c8a0", "c": "#e8d4a0", "v": "#7a8a24", "V": "#b4b850", "m": "#6a4a2a",
}

from iso_life_mdraw import KEYS
KEYS.update(P)
GLYPHS = {}


def reg(name):
    def d(fn):
        GLYPHS[name] = fn
        return fn
    return d


def slash(img, c="r"):
    """2 px wide diagonal slash from lower-left to upper-right across an art image."""
    h, w = img.shape[:2]
    line(img, 0, h - 1, w - 1, 0, c)
    line(img, 1, h - 1, w - 1, 1, c)
    return img


@reg("nopower")
def _():
    return art([
        "....yyy.",
        "...yYy..",
        "..yYYy..",
        ".yYYYyyy",
        "..yyYYy.",
        "....yYy.",
        "...yYy..",
        "...yy...",
    ], P)


@reg("nowater")
def _():
    return art([
        "...bb...",
        "...bb...",
        "..bBbb..",
        "..bBbb..",
        ".bBbbbb.",
        ".bBbbbb.",
        ".bbbbbb.",
        "..bbbb..",
    ], P)


@reg("nosewage")
def _():
    return art([
        "...nn...",
        "..nNNn..",
        "..nnnn..",
        ".nNNNNn.",
        ".nnnnnn.",
        "nNNNNNNn",
        "nnnnnnnn",
        ".nnnnnn.",
    ], P)


@reg("dirtywater")
def _():
    return art([
        "...vv...",
        "...vv...",
        "..vVvv..",
        "..vvNv..",
        ".vVvvvv.",
        ".vvNvvv.",
        ".vvvvNv.",
        "..vvvv..",
    ], P)


@reg("noroad")
def _():
    im = art([
        "...GG...",
        "...Gy...",
        "..GGgG..",
        "..GGyG..",
        ".GGGgGG.",
        ".GGGyGG.",
        "GGGGgGGG",
        "GGGGyGGG",
    ], P)
    return slash(im)


@reg("abandoned")
def _():
    im = art([
        "...RR...",
        "..RRRR..",
        ".RRRRRR.",
        "RRRRRRRR",
        ".cccccc.",
        ".cccccc.",
        ".cccccc.",
        ".cccccc.",
    ], P)
    line(im, 1, 4, 6, 7, "n")
    line(im, 1, 5, 5, 7, "n")
    line(im, 6, 4, 1, 7, "n")
    line(im, 6, 5, 2, 7, "n")
    return im


@reg("unhappy")
def _():
    return art([
        "..yyyy..",
        ".yYyyyy.",
        "yykyykyy",
        "yyyyyyyy",
        "yyykkyyy",
        "yykyykyy",
        ".yyyyyy.",
        "..yyyy..",
    ], P)


@reg("noworkers")
def _():
    im = art([
        "..yyyy..",
        ".yyyyyy.",
        "..ssss..",
        "..ssss..",
        ".bbbbbb.",
        "bbbbbbbb",
        "bbbbbbbb",
        "bb.bb.bb",
    ], P)
    return slash(im)


@reg("nocustomers")
def _():
    im = art([
        "rWrWrWrW",
        "rWrWrWrW",
        ".rWrWrW.",
        "cBBccnnc",
        "cBBccnnc",
        "cBBccnnc",
        "ccccnnnc",
        "cccccccc",
    ], P)
    return slash(im)


@reg("nogoods")
def _():
    return art([
        "nnnnnnnn",
        "nNNNNNNn",
        "nNkkkkNn",
        "nNkkkkNn",
        "nNkkkkNn",
        "nNkkkkNn",
        "nNNNNNNn",
        "nnnnnnnn",
    ], P)


@reg("garbage")
def _():
    return art([
        "...ll...",
        "..lLl...",
        "..llll..",
        ".llLlll.",
        "llLLllll",
        "llLlllll",
        "llllllll",
        ".llllll.",
    ], P)


@reg("fire")
def _():
    return art([
        "....o...",
        "...oo.o.",
        "..ooo.oo",
        "..ooyooo",
        ".ooyyooo",
        ".oyyYyoo",
        ".oyYYYyo",
        "..oyYyo.",
    ], P)


@reg("crime")
def _():
    return art([
        "..dddd..",
        ".dddddd.",
        "ssssssss",
        "kkkkkkkk",
        "kWkkkkWk",
        "kkkkkkkk",
        ".ssssss.",
        "..ssss..",
    ], P)


@reg("sick")
def _():
    return art([
        "...WW...",
        "...WW...",
        "...Wr...",
        "...Wr...",
        "...Wr...",
        "..WWrW..",
        ".WrrrrW.",
        "..WrrW..",
    ], P)


@reg("ambulance")
def _():
    return art([
        "..b.....",
        "WWWWWWWW",
        "WWrWWWdW",
        "WrrrWWBB",
        "WWrWWWBB",
        "WWWWWWWW",
        ".dd..dd.",
    ], P)


@reg("highrent")
def _():
    im = art([
        "...RR...",
        "..RRRR..",
        ".RRRRRR.",
        "RRRRRRRR",
        ".cccccc.",
        ".cccccc.",
        ".cccccc.",
        ".cccccc.",
    ], P)
    for (x, y) in [(2, 4), (3, 4), (4, 4), (2, 5), (2, 6), (3, 6), (4, 6), (4, 7), (3, 7), (2, 7)]:
        pass
    # tiny dollar: S shape 3x4 in the wall (cols 3..5, rows 4..7) + tick
    for (x, y) in [(3, 4), (4, 4), (5, 4), (3, 5), (3, 6), (4, 6), (5, 6), (5, 7), (3, 7), (4, 7)]:
        px(im, x, y, "l")
    px(im, 4, 3, "l")
    px(im, 4, 7, "l")
    return im


@reg("traffic")
def _():
    return art([
        "..rrrr..",
        ".rBBBBr.",
        "rrrrrrrr",
        "yrrrrrry",
        "rrrrrrrr",
        ".dd..dd.",
        ".dd..dd.",
    ], P)


@reg("airpollution")
def _():
    return art([
        "..GGG...",
        ".GGGGGG.",
        "GGGGGGGG",
        "gGGGGGGg",
        ".gggggg.",
        "..d.d.d.",
        ".d.d.d..",
    ], P)


@reg("groundpollution")
def _():
    return art([
        ".vvvvvv.",
        "vVVVVVVv",
        "yyyyyyyy",
        "vvkvvvvv",
        "vVvkvvvv",
        "yyyyyyyy",
        "vvvvvvvv",
        ".LL.LL..",
    ], P)


@reg("noise")
def _():
    return art([
        "...gG...",
        "..gGG.o.",
        "gggGG..o",
        "gggGG.o.",
        "gggGG..o",
        "..gGG.o.",
        "...gG...",
    ], P)


@reg("leveledup")
def _():
    return art([
        "...LL...",
        "..LLLL..",
        ".LLllLL.",
        "LLLllLLL",
        "...ll...",
        "...ll...",
        "...ll...",
        "...ll...",
    ], P)


@reg("collapsed")
def _():
    im = art([
        "..R.....",
        ".RR..R..",
        ".RRR.RR.",
        "gRRRgRRR",
        "ggRgggRg",
        "GgggGggg",
        "gGggggGg",
        "gggggggg",
    ], P)
    return im


@reg("noservice")
def _():
    return art([
        "dddddddd",
        "dWWrrWWd",
        "dWWrrWWd",
        "dWWrrWWd",
        ".dWWWWd.",
        ".dWrrWd.",
        "..dWWd..",
        "...dd...",
    ], P)


@reg("flooded")
def _():
    return art([
        "...n....",
        "..nnn...",
        ".nncnn..",
        "ncnnnnn.",
        "bBbBbBbB",
        "bbBbbBbb",
        "BbbbBbbb",
        "bbbbbbbb",
    ], P)
