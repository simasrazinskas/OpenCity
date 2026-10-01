"""Window icon and cursor glue for genui (the chrome itself is drawn at runtime, see OpenRA.Mods.City/UIArt)."""


def build_mod_icons(scales, mod):
    import uilogo
    uilogo.build(scales, mod)


def build_cursors(mod):
    import uicursors
    uicursors.build(mod)
