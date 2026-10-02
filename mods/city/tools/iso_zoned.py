#!/usr/bin/env python3
"""iso_zoned.py - ZONED (growable buildings) iso art generator: registry + output driver.

    python3 mods/city/tools/iso_zoned.py            # render everything to design/iso/zoned + manifest.json
    python3 mods/city/tools/iso_zoned.py --only res-low,com-low

Module map: iso_zoned_core (registry/render/clip), iso_zoned_lot (Lot builder + states),
iso_zoned_mats (facade/state materials), iso_zoned_props (dressing), iso_zoned_build (construction,
collapse, rubble), iso_zoned_apt (apartment block helper), zone models: iso_zoned_reslow, _resrow,
_resmed (med + mixed), _reshigh (high + low-rent), _com, _off, _ind (ind + warehouse), _sig;
output: iso_zoned_out (PNGs + manifest + vignettes).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import iso_zoned_core as C  # noqa: E402
import iso_zoned_reslow as RL  # noqa: E402
import iso_zoned_resrow as RR  # noqa: E402
import iso_zoned_resmed as RM  # noqa: E402
import iso_zoned_reshigh as RH  # noqa: E402
import iso_zoned_com as CM  # noqa: E402

C.register("res-low", "Residential low density", "1x1 2x1 1x2 2x2", RL.res_low)
C.register("res-row", "Residential row houses", "2x1 3x1 1x2 1x3 2x2", RR.res_row)
C.register("res-med", "Residential medium density", "2x2 3x2 2x3 3x3", RM.res_med)
C.register("res-high", "Residential high density", "2x2 3x2 2x3 3x3", RH.res_high)
C.register("res-lowrent", "Residential low rent", "2x2 3x2 2x3", RH.res_lowrent)
C.register("res-mixed", "Residential mixed use", "2x2 3x2 2x3", RM.res_mixed)
C.register("com-low", "Commercial low density", "1x1 2x1 1x2 2x2", CM.com_low)
C.register("com-high", "Commercial high density", "2x2 3x2 2x3 3x3 4x3 3x4", CM.com_high)

try:
    import iso_zoned_off as OF  # noqa: E402
    C.register("off", "Office low density", "1x1 2x1 1x2 2x2 2x3 3x2 3x3", OF.off_low)
    C.register("off-high", "Office high density", "2x2 3x2 2x3 3x3", OF.off_high)
except ImportError:
    pass
try:
    import iso_zoned_ind as IN  # noqa: E402
    C.register("ind", "Industrial", "1x1 2x2 3x2 2x3 3x3 4x3 3x4", IN.ind)
    C.register("warehouse", "Warehouses", "3x2 2x3 3x3 4x3 3x4", IN.warehouse)
except ImportError:
    pass

import iso_zoned_sig as SG  # noqa: E402
for _k, (_t, _fn) in SG.SIGS.items():
    C.register(_k, "Signature: " + _t, "3x3", _fn)


if __name__ == "__main__":
    import iso_zoned_out
    iso_zoned_out.main(sys.argv[1:])
