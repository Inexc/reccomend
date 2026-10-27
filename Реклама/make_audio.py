# -*- coding: utf-8 -*-
"""Синтез музыки, звуковых эффектов и сведение с озвучкой для ролика MovieBook."""
import math
import os
import subprocess
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
AUDIO = os.path.join(HERE, "audio")
OUT = os.path.join(HERE, "output")
os.makedirs(OUT, exist_ok=True)

SR = 48000
DUR = 30.0
N = int(SR * DUR)
rng = np.random.default_rng(42)

mixL = np.zeros(N, dtype=np.float64)
mixR = np.zeros(N, dtype=np.float64)
musicL = np.zeros(N, dtype=np.float64)
musicR = np.zeros(N, dtype=np.float64)


def tarr(dur):
    return np.arange(int(dur * SR), dtype=np.float64) / SR


def add(sig, at, gain=1.0, pan=0.0, target=None):
    if target is None:
        target = "music"
    i0 = int(at * SR)
    if i0 >= N:
        return
    n = min(len(sig), N - i0)
    if n <= 0:
        return
    th = (pan + 1) * math.pi / 4
    gl, gr = math.cos(th) * gain, math.sin(th) * gain
    if target == "music":
        musicL[i0:i0 + n] += sig[:n] * gl
        musicR[i0:i0 + n] += sig[:n] * gr
    else:
        mixL[i0:i0 + n] += sig[:n] * gl
        mixR[i0:i0 + n] += sig[:n] * gr


def fft_filter(x, low=None, high=None):
    n = len(x)
    if n == 0:
        return x
    X = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(n, 1 / SR)
    mask = np.ones_like(freqs)
    if low is not None:
        mask *= 1 / (1 + np.exp(-(freqs - low) / max(1, low * 0.15)))
    if high is not None:
        mask *= 1 / (1 + np.exp((freqs - high) / max(1, high * 0.12)))
    return np.fft.irfft(X * mask, n)


def env_ad(t, attack=0.01, decay=4.0):
    return (1 - np.exp(-t / max(1e-4, attack))) * np.exp(-t * decay)


def kick(dur=0.45):
    t = tarr(dur)
    f = 45 + 95 * np.exp(-t * 28)
    phase = 2 * math.pi * np.cumsum(f) / SR
    body = np.sin(phase) * np.exp(-t * 8.5)
    click = rng.normal(0, 1, len(t)) * np.exp(-t * 180) * 0.35
    return np.tanh((body + click) * 1.4)


def snare(dur=0.35):
    t = tarr(dur)
    noise = fft_filter(rng.normal(0, 1, len(t)), 900, 9000)
    noise = noise / (np.max(np.abs(noise)) + 1e-9)
    body = np.sin(2 * math.pi * 185 * t) * np.exp(-t * 22)
    return (noise * np.exp(-t * 15) * 0.75 + body * 0.5)


def hat(dur=0.09, open_=False):
    t = tarr(dur)
    noise = fft_filter(rng.normal(0, 1, len(t)), 6500, 16000)
    noise = noise / (np.max(np.abs(noise)) + 1e-9)
    return noise * np.exp(-t * (28 if open_ else 70))


def bass(f, dur=0.5, gain=1.0):
    t = tarr(dur)
    env = (1 - np.exp(-t * 400)) * np.exp(-t * 4.5)
    sig = np.sin(2 * math.pi * f * t) + 0.35 * np.sin(2 * math.pi * 2 * f * t) + 0.12 * np.sin(2 * math.pi * 3 * f * t)
    return np.tanh(sig * env * 1.2) * gain


def pluck(f, dur=0.55):
    t = tarr(dur)
    env = np.exp(-t * 7.0) * (1 - np.exp(-t * 300))
    sig = np.sin(2 * math.pi * f * t) + 0.35 * np.sin(2 * math.pi * 2 * f * t)
    return sig * env


def pad(freqs, dur, attack=0.6, release=0.8):
    t = tarr(dur)
    env = np.minimum(1, t / max(1e-4, attack)) * np.minimum(1, (dur - t) / max(1e-4, release))
    env = np.clip(env, 0, 1) ** 1.5
    sig = np.zeros_like(t)
    for i, f in enumerate(freqs):
        det = 1.0 + (i - 1) * 0.0015
        sig += np.sin(2 * math.pi * f * det * t + i * 0.7)
        sig += 0.35 * np.sin(2 * math.pi * 2 * f * det * t + i * 1.1)
        sig += 0.16 * np.sin(2 * math.pi * 3 * f * det * t + i * 1.7)
    sig /= max(1, len(freqs))
    lfo = 1 + 0.08 * np.sin(2 * math.pi * 0.13 * t)
    return sig * env * lfo


def riser(dur=1.6):
    t = tarr(dur)
    noise = fft_filter(rng.normal(0, 1, len(t)), 300, 9000)
    noise = noise / (np.max(np.abs(noise)) + 1e-9)
    sweep = np.sin(2 * math.pi * (180 + 1400 * (t / dur) ** 2) * t)
    env = (t / dur) ** 2.2
    return (noise * 0.7 + sweep * 0.35) * env


def impact(dur=1.4):
    t = tarr(dur)
    f = 32 + 50 * np.exp(-t * 6)
    phase = 2 * math.pi * np.cumsum(f) / SR
    boom = np.sin(phase) * np.exp(-t * 3.2)
    noise = fft_filter(rng.normal(0, 1, len(t)), 40, 500)
    noise = noise / (np.max(np.abs(noise)) + 1e-9)
    return boom + noise * np.exp(-t * 7) * 0.35


def whoosh(dur=0.6):
    t = tarr(dur)
    noise = fft_filter(rng.normal(0, 1, len(t)), 350, 7000)
    noise = noise / (np.max(np.abs(noise)) + 1e-9)
    env = np.sin(math.pi * np.clip(t / dur, 0, 1)) ** 2
    return noise * env


def click():
    t = tarr(0.035)
    noise = rng.normal(0, 1, len(t)) * np.exp(-t * 220)
    tone = np.sin(2 * math.pi * 2100 * t) * np.exp(-t * 120)
    return (noise * 0.5 + tone * 0.5)


def chime(dur=1.0):
    t = tarr(dur)
    env = np.exp(-t * 3.5) * (1 - np.exp(-t * 300))
    sig = (np.sin(2 * math.pi * 880 * t) + 0.6 * np.sin(2 * math.pi * 1320 * t) +
           0.35 * np.sin(2 * math.pi * 1760 * t) + 0.2 * np.sin(2 * math.pi * 2640 * t))
    return sig * env / 2.0


def heartbeat(dur=0.5):
    t = tarr(dur)
    thump = lambda off: np.sin(2 * math.pi * 52 * (t - off)) * np.exp(-np.maximum(0, t - off) * 12) * (t >= off)
    return thump(0.0) * 0.9 + thump(0.22) * 0.55


def smooth(x, n):
    n = max(1, int(n))
    c = np.cumsum(np.insert(x, 0, 0))
    y = (c[n:] - c[:-n]) / n
    pad = n // 2
    return np.pad(y, (pad, n - 1 - pad), mode="edge")


def add_reverb(sig, delays=(0.07, 0.13, 0.21, 0.34), gains=(0.28, 0.2, 0.13, 0.08)):
    out = sig.copy()
    for d, g in zip(delays, gains):
        n = int(d * SR)
        if n < len(sig):
            out[n:] += sig[:-n] * g
    return out


# ---------------------------------------------------------------------------
# Аранжировка
# ---------------------------------------------------------------------------
CH = {
    "Am": [110.00, 130.81, 164.81],
    "F": [87.31, 110.00, 130.81],
    "C": [130.81, 164.81, 196.00],
    "G": [98.00, 123.47, 146.83],
}
sections = [
    (2.2, 4.4, "Am"), (4.4, 7.2, "F"), (7.2, 11.8, "C"), (11.8, 15.6, "G"),
    (15.6, 19.2, "Am"), (19.2, 22.4, "F"), (22.4, 25.2, "G"), (25.2, 30.0, "Am"),
]

# вступление: дрон и сердцебиение
t0 = tarr(4.6)
drone = (np.sin(2 * math.pi * 55 * t0) + 0.5 * np.sin(2 * math.pi * 82.5 * t0) +
         0.25 * np.sin(2 * math.pi * 110 * t0)) * np.minimum(1, t0 / 1.2) * np.exp(-t0 * 0.18)
add(drone, 0.0, 0.10, -0.1)
add(drone, 0.0, 0.10, 0.1)
for bt in (0.18, 0.78, 1.38, 1.98):
    add(heartbeat(), bt, 0.22, 0.0)

# райзер и импакт перед битом
add(riser(1.5), 0.9, 0.20, 0.0)
add(impact(1.6), 2.18, 0.42, 0.0)
add(whoosh(0.7), 2.05, 0.16, 0.0)

# основной бит
beat = 0.6
start, stop = 2.2, 25.2
beat_times = np.arange(start, stop, beat)
for i, bt in enumerate(beat_times):
    add(kick(), bt, 0.50, 0.0)
    if i % 4 in (1, 3):
        add(snare(), bt, 0.24, 0.0)
    # хэты
    add(hat(0.08, False), bt, 0.10, 0.35)
    add(hat(0.08, False), bt + beat / 2, 0.07, -0.35)
    if i % 2 == 0:
        add(hat(0.12, True), bt + beat / 2, 0.055, 0.15)

# бас и пэды по секциям
for s0, s1, name in sections:
    chord = CH[name]
    root = chord[0] / 2
    # пэд
    dur = s1 - s0
    p = pad(chord, dur, attack=0.7, release=0.9)
    add(p, s0, 0.12, -0.45)
    p2 = pad([f * 1.004 for f in chord], dur, attack=0.9, release=1.0)
    add(p2, s0, 0.12, 0.45)
    # бас 8-ми
    bt = s0
    k = 0
    while bt < s1 - 0.05:
        add(bass(root * (1.0 if k % 4 != 3 else 1.5), 0.5), bt, 0.28, 0.0)
        bt += beat / 2
        k += 1
    # арпеджио
    bt = s0
    k = 0
    while bt < s1 - 0.05:
        f = chord[(k * 2) % len(chord)] * 2
        sig = pluck(f, 0.5) * 0.35
        add(sig, bt, 0.10, 0.55 if k % 2 else -0.55)
        add(sig, bt + 0.18, 0.035, -0.55 if k % 2 else 0.55)
        bt += beat / 4
        k += 1

# переходы: whoosh + impact
for tt in (4.4, 7.2, 11.8, 15.6, 19.2, 22.4, 25.2):
    add(whoosh(0.55), tt - 0.45, 0.13, 0.0)
    add(impact(1.2), tt, 0.30, 0.0)

# UI-звуки
add(click(), 7.55, 0.10, 0.0)
add(click(), 8.02, 0.18, 0.0)
add(chime(), 8.10, 0.20, 0.0)
add(click(), 8.24, 0.10, 0.0)
add(click(), 13.55, 0.10, 0.0)
add(click(), 19.2, 0.10, 0.0)

# печать в поиске
for i in range(11):
    tt = 19.55 + i * 0.075
    add(click(), tt, 0.055, 0.15)
add(chime(), 20.05, 0.16, 0.0)
add(click(), 21.0, 0.10, 0.0)

# финальный аккорд и шиммер
add(impact(2.0), 25.2, 0.48, 0.0)
final_pad = pad(CH["Am"], 4.7, attack=0.3, release=1.8)
add(final_pad, 25.2, 0.16, -0.4)
add(pad([f * 1.005 for f in CH["Am"]], 4.7, attack=0.4, release=1.9), 25.2, 0.16, 0.4)
for i, f in enumerate([880, 1108.73, 1318.51, 1760]):
    t = tarr(4.0)
    shimmer = np.sin(2 * math.pi * f * t) * np.exp(-t * 1.4) * 0.035
    add(shimmer, 25.4 + i * 0.12, 1.0, (-0.6 + i * 0.4))

# общая обработка музыки
music = np.stack([musicL, musicR], axis=1)
# мягкая компрессия/сатурация
music = np.tanh(music * 1.1) / 1.1
# реверб
music[:, 0] = add_reverb(music[:, 0])
music[:, 1] = add_reverb(music[:, 1])
# фейд-аут в конце
fade = np.ones(N)
fade[-int(1.2 * SR):] = np.linspace(1, 0, int(1.2 * SR))
music *= fade[:, None]

# ---------------------------------------------------------------------------
# Озвучка
# ---------------------------------------------------------------------------
VO_FILES = [
    ("vo2_1.mp3", 0.15),
    ("vo2_2.mp3", 4.50),
    ("vo2_3.mp3", 7.35),
    ("vo2_4.mp3", 11.90),
    ("vo2_5.mp3", 16.60),
    ("vo2_6.mp3", 19.45),
    ("vo3_8.mp3", 25.70),
]


def mp3_to_wav(path):
    wav = path + ".wav"
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", path, "-ar", str(SR), "-ac", "1", wav],
                   check=True)
    return wav


def read_wav(path):
    with wave.open(path, "rb") as w:
        n = w.getnframes()
        data = w.readframes(n)
        sw = w.getsampwidth()
        if sw == 2:
            arr = np.frombuffer(data, dtype=np.int16).astype(np.float64) / 32768.0
        elif sw == 4:
            arr = np.frombuffer(data, dtype=np.int32).astype(np.float64) / 2147483648.0
        else:
            raise ValueError("unsupported sample width")
        if w.getnchannels() == 2:
            arr = arr.reshape(-1, 2).mean(axis=1)
    return arr


vo = np.zeros(N, dtype=np.float64)
vo_env = np.zeros(N, dtype=np.float64)
for name, at in VO_FILES:
    path = os.path.join(AUDIO, name)
    if not os.path.exists(path) or os.path.getsize(path) < 1000:
        print("missing", name, flush=True)
        continue
    wav = mp3_to_wav(path)
    sig = read_wav(wav)
    # нормализация и мягкие края
    sig = sig - np.mean(sig)
    peak = np.max(np.abs(sig)) + 1e-9
    sig = sig / peak * 0.85
    n = len(sig)
    fade_n = int(0.012 * SR)
    if n > 2 * fade_n:
        sig[:fade_n] *= np.linspace(0, 1, fade_n)
        sig[-fade_n:] *= np.linspace(1, 0, fade_n)
    i0 = int(at * SR)
    n2 = min(n, N - i0)
    if n2 <= 0:
        continue
    vo[i0:i0 + n2] += sig[:n2]
    # конверт для дакинга
    e = np.abs(sig[:n2])
    e = smooth(e, 1200)
    vo_env[i0:i0 + n2] = np.maximum(vo_env[i0:i0 + n2], e)

# дакинг музыки под голос
duck = 1 - 0.38 * np.clip(vo_env / (np.max(vo_env) + 1e-9), 0, 1)
duck = smooth(duck, 2400)
music *= duck[:, None]

# финальный микс
final = music * 0.78 + np.stack([vo, vo], axis=1) * 1.0
# высокочастотный фильтр и нормализация
final[:, 0] = fft_filter(final[:, 0], low=28)
final[:, 1] = fft_filter(final[:, 1], low=28)
peak = np.max(np.abs(final)) + 1e-9
final = final / peak * 0.95
final = np.tanh(final * 1.05) / 1.05
# фейд в самом конце
final[-int(0.25 * SR):] *= np.linspace(1, 0, int(0.25 * SR))[:, None]

out_path = os.path.join(OUT, "soundtrack.wav")
pcm = (np.clip(final, -1, 1) * 32767).astype(np.int16)
with wave.open(out_path, "wb") as w:
    w.setnchannels(2)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(pcm.tobytes())
print("wrote", out_path, flush=True)
print("duration", N / SR, "peak", np.max(np.abs(final)), flush=True)
