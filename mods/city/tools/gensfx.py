#!/usr/bin/env python3
"""
gensfx - synthesises the OpenCity UI / city sound effects and audio/notifications.yaml.

  python3 mods/city/tools/gensfx.py

Python 3 standard library only (wave, math, random, struct). 16-bit mono PCM, 44.1 kHz.
Deterministic: every sound uses its own seeded RNG.
"""

import math
import os
import random
import struct
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.dirname(HERE)
OUT = os.path.join(MOD, 'bits', 'audio')
SR = 44100
TAU = 2 * math.pi


def n_samples(sec):
    return int(SR * sec)


def silence(sec):
    return [0.0] * n_samples(sec)


def env_exp(t, decay):
    return math.exp(-t * decay)


def tone(freq, sec, decay=8.0, attack=0.002, shape='sine', phase=0.0, bend=None):
    """A decaying tone. bend = (end_freq): exponential glide from freq to end_freq."""
    out = []
    ph = phase
    n = n_samples(sec)
    for i in range(n):
        t = i / SR
        f = freq if bend is None else freq * (bend / freq) ** (i / n)
        ph += TAU * f / SR
        if shape == 'sine':
            v = math.sin(ph)
        elif shape == 'tri':
            v = 2 / math.pi * math.asin(math.sin(ph))
        elif shape == 'square':
            v = 1.0 if math.sin(ph) >= 0 else -1.0
        else:
            v = math.sin(ph)
        a = min(1.0, t / attack) if attack > 0 else 1.0
        out.append(v * a * env_exp(t, decay))
    return out


def bell(freq, sec, decay=5.0, bright=0.5):
    """Soft bell: a few inharmonic partials with faster decay for the higher ones."""
    out = [0.0] * n_samples(sec)
    for mult, amp, dm in ((1.0, 1.0, 1.0), (2.0, 0.45 * bright + 0.1, 1.6), (2.76, 0.28 * bright, 2.2), (5.4, 0.12 * bright, 3.5)):
        p = tone(freq * mult, sec, decay * dm, attack=0.002)
        for i in range(len(out)):
            out[i] += p[i] * amp
    return out


def noise(sec, seed, decay=0.0, attack=0.0):
    r = random.Random(seed)
    out = []
    for i in range(n_samples(sec)):
        t = i / SR
        a = min(1.0, t / attack) if attack > 0 else 1.0
        out.append(r.uniform(-1, 1) * a * (math.exp(-t * decay) if decay else 1.0))
    return out


def lowpass(x, cutoff):
    a = 1 - math.exp(-TAU * cutoff / SR)
    y, s = [], 0.0
    for v in x:
        s += a * (v - s)
        y.append(s)
    return y


def highpass(x, cutoff):
    lp = lowpass(x, cutoff)
    return [v - l for v, l in zip(x, lp)]


def bandpass(x, lo, hi):
    return highpass(lowpass(x, hi), lo)


def mixdown(*parts):
    n = max(len(p) for _, p in parts)
    out = [0.0] * n
    for gain, p in parts:
        for i, v in enumerate(p):
            out[i] += v * gain
    return out


def delay(x, sec):
    return silence(sec) + x


def scale(x, g):
    return [v * g for v in x]


def fade(x, ms=6):
    n = min(len(x) // 2, int(SR * ms / 1000))
    for i in range(n):
        k = i / n
        x[i] *= k
        x[-1 - i] *= k
    return x


def echo(x, sec, gain, times=3):
    out = x + silence(sec * times)
    for k in range(1, times + 1):
        d = int(SR * sec * k)
        for i, v in enumerate(x):
            out[i + d] += v * (gain ** k)
    return out


def normalise(x, peak=0.8):
    m = max(abs(v) for v in x) or 1.0
    return [v / m * peak for v in x]


def save(name, x, peak=0.8):
    x = fade(normalise(x, peak))
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + '.wav'), 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, v)) * 32767)) for v in x))


# ---- the sounds ---------------------------------------------------------------------

def s_click():
    return mixdown((1.0, tone(2100, 0.05, 90, shape='sine')),
                   (0.35, tone(1050, 0.05, 70, shape='tri')),
                   (0.25, lowpass(noise(0.015, 1, 160), 5000)))


def s_click_disabled():
    return mixdown((1.0, tone(190, 0.09, 38, shape='tri', bend=150)),
                   (0.4, lowpass(noise(0.03, 2, 90), 900)))


def s_chat():
    return mixdown((1.0, bell(1318, 0.18, 17, 0.3)), (0.8, delay(bell(1760, 0.2, 15, 0.3), 0.07)))


def s_joined():
    return mixdown((1.0, bell(659, 0.5, 7, 0.5)), (0.9, delay(bell(988, 0.55, 6.5, 0.5), 0.1)))


def s_left():
    return mixdown((1.0, bell(988, 0.4, 8, 0.4)), (0.9, delay(bell(659, 0.5, 7, 0.4), 0.1)))


def s_lobby_option():
    return mixdown((1.0, tone(1560, 0.07, 60, shape='sine')), (0.4, tone(2340, 0.05, 80)))


def s_build():
    thump = tone(150, 0.32, 13, attack=0.003, bend=48)
    knock = lowpass(noise(0.12, 3, 38, 0.001), 1800)
    click = tone(900, 0.03, 120)
    return mixdown((1.0, thump), (0.55, knock), (0.25, click), (0.25, delay(tone(95, 0.2, 18, bend=60), 0.09)))


def s_bulldoze():
    rumble = lowpass(noise(0.42, 4, 6.5, 0.004), 650)
    chunk = [v * (0.55 + 0.45 * math.sin(TAU * 26 * i / SR)) for i, v in enumerate(rumble)]
    crack = highpass(lowpass(noise(0.2, 5, 22, 0.001), 3500), 500)
    return mixdown((1.0, chunk), (0.7, crack), (0.5, tone(80, 0.3, 11, bend=45)))


def s_zone():
    n = bandpass(noise(0.26, 6, 0.0, 0.07), 1400, 3600)
    swell = [v * math.sin(math.pi * i / len(n)) ** 1.5 for i, v in enumerate(n)]
    return mixdown((1.0, swell), (0.25, delay(tone(1480, 0.1, 30), 0.03)))


def s_road():
    grit = lowpass(noise(0.2, 7, 20, 0.001), 2600)
    scrape = [v * (0.6 + 0.4 * math.sin(TAU * 70 * i / SR)) for i, v in enumerate(bandpass(noise(0.18, 8, 14, 0.01), 500, 1800))]
    return mixdown((1.0, grit), (0.8, scrape), (0.7, tone(110, 0.14, 24, bend=70)))


def s_money():
    a = bell(1976, 0.5, 9, 0.9)
    b = delay(bell(2637, 0.55, 8, 0.9), 0.07)
    return mixdown((0.8, a), (1.0, b), (0.2, lowpass(noise(0.01, 9, 300), 9000)))


def s_milestone():
    notes = (523.25, 659.25, 783.99, 1046.5)
    parts = []
    for i, f in enumerate(notes):
        parts.append((0.8, delay(bell(f, 0.9, 4.0, 0.55), 0.11 * i)))
        parts.append((0.25, delay(tone(f / 2, 0.8, 4.5, shape='tri'), 0.11 * i)))
    chord = mixdown(*[(0.45, delay(bell(f, 1.5, 2.6, 0.4), 0.46)) for f in (523.25, 659.25, 783.99, 1046.5)])
    return echo(mixdown(*parts, (1.0, chord)), 0.12, 0.28, 3)


def s_error():
    def buzz(freq, sec):
        return lowpass(tone(freq, sec, 14, attack=0.004, shape='square'), 700)
    return mixdown((1.0, buzz(135, 0.12)), (1.0, delay(buzz(120, 0.16), 0.13)))


SOUNDS = {
    'click': (s_click, 0.5), 'click-disabled': (s_click_disabled, 0.55), 'chat': (s_chat, 0.5),
    'player-joined': (s_joined, 0.6), 'player-left': (s_left, 0.6), 'lobby-option': (s_lobby_option, 0.5),
    'city-build': (s_build, 0.85), 'city-bulldoze': (s_bulldoze, 0.85), 'city-zone': (s_zone, 0.55),
    'city-money': (s_money, 0.6), 'city-milestone': (s_milestone, 0.75), 'city-error': (s_error, 0.6),
    'city-road': (s_road, 0.7),
}

NOTIFICATIONS = """Sounds:
	Notifications:
		# UI (referenced by metrics.yaml / common widgets)
		ChatLine: chat
		ClickSound: click
		ClickDisabledSound: click-disabled
		PlayerJoined: player-joined
		PlayerLeft: player-left
		LobbyOptionChanged: lobby-option
		# City gameplay. From C#: Game.Sound.PlayNotification(Game.ModData.DefaultRules, null, "Sounds", "CityBuild", null)
		CityBuild: city-build
		CityBulldoze: city-bulldoze
		CityZone: city-zone
		CityMoney: city-money
		CityMilestone: city-milestone
		CityError: city-error
		CityRoad: city-road
"""


def main():
    for name, (fn, peak) in SOUNDS.items():
        save(name, fn(), peak)
    # remove sounds that are no longer referenced (old bootstrap names are reused, nothing to delete)
    with open(os.path.join(MOD, 'audio', 'notifications.yaml'), 'w') as f:
        f.write(NOTIFICATIONS)
    print('gensfx: wrote %d sounds to %s' % (len(SOUNDS), OUT))


if __name__ == '__main__':
    main()
