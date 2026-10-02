"""Deterministic integer hash noise (no global RNG state)."""
import numpy as np

M32 = np.uint64(0xFFFFFFFF)


def hash2(ix, iy, seed=0):
    """Hash integer arrays to floats in [0, 1)."""
    x = np.asarray(ix, np.int64).astype(np.uint64) & M32
    y = np.asarray(iy, np.int64).astype(np.uint64) & M32
    h = (x * np.uint64(374761393) + y * np.uint64(668265263) + np.uint64(seed * 2246822519 & 0xFFFFFFFF)) & M32
    h = ((h ^ (h >> np.uint64(13))) * np.uint64(1274126177)) & M32
    h = h ^ (h >> np.uint64(16))
    return (h & np.uint64(0xFFFFFF)).astype(np.float64) / float(0x1000000)


def hash1(i, seed=0):
    return hash2(i, 0, seed)


def value2(x, y, scale=1.0, seed=0):
    """Smooth value noise in [0, 1) at float coords (x, y) / scale."""
    x = np.asarray(x, np.float64) / scale
    y = np.asarray(y, np.float64) / scale
    x0, y0 = np.floor(x), np.floor(y)
    fx, fy = x - x0, y - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a = hash2(x0, y0, seed)
    b = hash2(x0 + 1, y0, seed)
    c = hash2(x0, y0 + 1, seed)
    d = hash2(x0 + 1, y0 + 1, seed)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(x, y, scale=8.0, octaves=3, seed=0):
    tot, amp, norm = 0.0, 1.0, 0.0
    for o in range(octaves):
        tot = tot + value2(x, y, scale / (2 ** o), seed + o * 101) * amp
        norm += amp
        amp *= 0.5
    return tot / norm


class Rng:
    """Tiny seeded RNG wrapper for generators (deterministic)."""

    def __init__(self, seed):
        self.r = np.random.default_rng(seed)

    def uniform(self, a=0.0, b=1.0):
        return float(self.r.uniform(a, b))

    def randint(self, a, b):
        return int(self.r.integers(a, b + 1))

    def choice(self, seq):
        return seq[int(self.r.integers(0, len(seq)))]

    def chance(self, p):
        return bool(self.r.random() < p)
