"""Imports every CIVIC model module so they register themselves (see iso_civic_states.model)."""
import importlib

MODULES = ["iso_civic_m_power", "iso_civic_m_water", "iso_civic_m_safety", "iso_civic_m_health",
           "iso_civic_m_edu", "iso_civic_m_parks", "iso_civic_m_garbage", "iso_civic_m_comms",
           "iso_civic_m_industry", "iso_civic_m_areas", "iso_civic_m_transit"]
for _m in MODULES:
    try:
        importlib.import_module(_m)
    except ModuleNotFoundError as e:
        if e.name != _m:
            raise
