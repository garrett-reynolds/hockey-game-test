# Writes the made-for-this-project Round sounds as 16-bit PCM WAV (44.1 kHz). Released CC0.
#   python Tools/make_sounds.py <out dir>                     -> goal-horn.wav, buzzer.wav (pure synthesis)
#   python Tools/make_sounds.py <out dir> --voice ooo.wav     -> also groan.wav (layered, gliding copies of one voice)
#   python Tools/make_sounds.py <out dir> --crowd crowd.wav   -> also crowd-loop.wav (seamless loop cut from a recording)
#   python Tools/make_sounds.py <out dir> --impact <dir>      -> also glove-catch.wav, blocker-hit.wav (Kenney Impact Sounds mixes)
# goal-horn and buzzer are pure synthesis and are always written. post-ping.wav is a real recording now (see CREDITS.md).
# --impact <dir> needs these Kenney "Impact Sounds" clips (https://kenney.nl/assets/impact-sounds, CC0) decoded to WAV:
#   impactPunch_heavy_002, impactSoft_medium_003, impactWood_medium_002, impactWood_light_000
#   e.g. for f in impactPunch_heavy_002 ...; do ffmpeg -i Audio/$f.ogg -ac 1 -ar 44100 <dir>/$f.wav; done
# Input WAVs: decode first with ffmpeg, e.g. ffmpeg -i oooooooooo.ogg -ac 1 -ar 44100 ooo.wav
# Needs numpy. Seeded, so the output is the same on every run.
import argparse, os, wave
import numpy as np

RATE = 44100
PEAK = 10 ** (-3 / 20)  # one-shots peak at -3 dBFS so net-hit + goal-horn together stay clear of clipping


def write(path, x):
    """x: (n,) mono or (n, 2) stereo floats in -1..1."""
    x = np.clip(x, -1, 1)
    ch = 1 if x.ndim == 1 else x.shape[1]
    with wave.open(path, "wb") as w:
        w.setnchannels(ch); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes((x * 32767).round().astype("<i2").tobytes())
    print("wrote", path, "%.2f s" % (len(x) / RATE))


def read(path):
    with wave.open(path, "rb") as w:
        assert w.getsampwidth() == 2 and w.getframerate() == RATE, path + ": need 16-bit 44.1 kHz WAV"
        x = np.frombuffer(w.readframes(w.getnframes()), "<i2").astype(np.float64) / 32768
        return x.reshape(-1, w.getnchannels()) if w.getnchannels() > 1 else x


def normalize(x, peak=PEAK):
    return x * (peak / np.abs(x).max())


def envelope(n, attack, release):
    e = np.ones(n)
    a, r = int(attack * RATE), int(release * RATE)
    if a: e[:a] = np.linspace(0, 1, a)
    if r: e[-r:] *= np.linspace(1, 0, r) ** 2
    return e


def saw(phase, max_harmonic_hz, f0):
    """Band-limited sawtooth from a phase array (cycles) and its nominal pitch."""
    out = np.zeros_like(phase)
    for k in range(1, int(max_harmonic_hz / f0) + 1):
        out += np.sin(2 * np.pi * k * phase) / k
    return out


def lowpass(x, hz):
    a = np.exp(-2 * np.pi * hz / RATE)
    y = np.empty_like(x); acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc; y[i] = acc
    return y


def room(x, wet=0.22):
    """Small arena-ish echo: a few feedback combs, mixed under the dry sound."""
    y = np.zeros(len(x) + int(0.6 * RATE)); y[:len(x)] += x
    tail = np.zeros_like(y)
    for delay_s, fb in ((0.0297, 0.72), (0.0371, 0.7), (0.0411, 0.68), (0.0437, 0.66)):
        d = int(delay_s * RATE); c = np.zeros_like(y)
        src = np.concatenate([x, np.zeros(len(y) - len(x))])
        for i in range(d, len(y), d):  # block-wise feedback comb
            m = min(d, len(y) - i)
            c[i:i + m] = src[i - d:i - d + m] + fb * c[i - d:i - d + m]
        tail += c
    tail = lowpass(tail, 3000)
    return y + wet * tail / 4


def goal_horn():
    """Arena goal horn: a low Bb major chord of detuned saws, quick attack, 2.2 s."""
    rng = np.random.default_rng(22)
    dur = 2.2; n = int(dur * RATE); t = np.arange(n) / RATE
    scoop = 1 - 0.03 * np.exp(-t / 0.05)                 # starts a touch flat, settles fast
    vib = 1 + 0.002 * np.sin(2 * np.pi * 5.5 * t)
    out = np.zeros(n)
    for f0 in (116.54, 146.83, 174.61):                    # Bb2, D3, F3
        for detune in (-0.004, 0.0, 0.005):
            f = f0 * (1 + detune) * scoop * vib
            phase = np.cumsum(f) / RATE + rng.random()
            out += saw(phase, 5000, f0)
    out = lowpass(out, 2200) * envelope(n, 0.06, 0.35)
    return normalize(room(out))


def buzzer():
    """End-of-time arena buzzer: rough square+saw around 120 Hz, 1.0 s, hard on and off."""
    dur = 1.0; n = int(dur * RATE); t = np.arange(n) / RATE
    sq = np.zeros(n)
    for k in range(1, 40, 2):
        sq += np.sin(2 * np.pi * k * 120 * t) / k
    sw = saw(240.6 * t, 6000, 240.6)
    x = 0.7 * sq + 0.35 * sw
    x *= 1 + 0.25 * np.sin(2 * np.pi * 60 * t)            # electromechanical rattle
    x = np.tanh(2.2 * x)                                   # harsh, like a cheap horn driver
    x = lowpass(x, 4500) * envelope(n, 0.008, 0.06)
    return normalize(room(x, 0.15))


def glove_catch(punch, soft):
    """Puck snapped into the catching glove: a dull leather pop, tails trimmed short."""
    return normalize(fit(mix((1.0, punch), (0.35, soft)), 0.35, 0.12))


def blocker_hit(wood, light):
    """Puck off the blocker's hard face: a bright two-layer knock, harder than the pad and the glove."""
    return normalize(fit(mix((1.0, wood), (0.5, light)), 0.4, 0.12))


def mix(*parts):
    n = max(len(p) for _, p in parts); out = np.zeros(n)
    for g, p in parts: out[:len(p)] += g * p
    return out


def fit(x, seconds, release):
    """Cut or pad x to seconds, fading out over the last release seconds."""
    n = int(seconds * RATE)
    x = np.concatenate([x, np.zeros(max(0, n - len(x)))])[:n]
    return x * envelope(n, 0.0, release)


def groan(voice):
    """Crowd "awww": 8 staggered, detuned copies of one sung "ooo", each gliding down about 4 semitones."""
    rng = np.random.default_rng(7)
    if voice.ndim > 1: voice = voice.mean(axis=1)
    dur = 2.0; n = int(dur * RATE); t = np.arange(n) / RATE
    out = np.zeros((n, 2))
    for i in range(8):
        r0 = rng.uniform(0.72, 1.05)                       # deeper copies read as more people
        start = int(rng.uniform(0, 0.18) * RATE)
        glide = 2 ** (-rng.uniform(3, 4.5) / 12 * np.clip(t / dur, 0, 1))
        wobble = 1 + 0.006 * np.sin(2 * np.pi * rng.uniform(4, 6) * t + rng.random() * 6)
        pos = rng.uniform(0.05, 0.12) * RATE + np.cumsum(r0 * glide * wobble)  # skip the voice's onset
        pos = pos[pos < len(voice) - 1]
        layer = np.interp(pos, np.arange(len(voice)), voice)
        m = min(len(layer), n - start)
        pan = rng.uniform(0.2, 0.8)
        out[start:start + m, 0] += layer[:m] * (1 - pan)
        out[start:start + m, 1] += layer[:m] * pan
    for c in range(2):
        out[:, c] = lowpass(out[:, c], 2500) * envelope(n, 0.12, 0.7)
    return normalize(out)


def crowd_loop(crowd, length=16.0, fade=1.5, gain_db=-6):
    """Seamless loop: loop[0:F] = seg[0:F]*fade_in + seg[L:L+F]*fade_out (equal power), loop[F:L] = seg[F:L].
    The end of the loop (seg[L-1]) runs straight on into the start (≈ seg[L]), so there is no click or gap."""
    if crowd.ndim == 1: crowd = crowd[:, None]
    L, F = int(length * RATE), int(fade * RATE)
    seg = crowd[int(2 * RATE):int(2 * RATE) + L + F]      # skip the first 2 s of the recording
    k = np.linspace(0, np.pi / 2, F)[:, None]
    loop = seg[:L].copy()
    loop[:F] = seg[:F] * np.sin(k) + seg[L:L + F] * np.cos(k)
    return loop * 10 ** (gain_db / 20)


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("out")
    p.add_argument("--voice", help="16-bit 44.1 kHz WAV of one voice singing 'ooo' (makes groan.wav)")
    p.add_argument("--crowd", help="16-bit 44.1 kHz WAV of steady crowd noise (makes crowd-loop.wav)")
    p.add_argument("--impact", help="folder of Kenney Impact Sounds clips decoded to WAV (makes glove-catch.wav, blocker-hit.wav)")
    a = p.parse_args()
    os.makedirs(a.out, exist_ok=True)
    write(os.path.join(a.out, "goal-horn.wav"), goal_horn())
    write(os.path.join(a.out, "buzzer.wav"), buzzer())
    if a.impact:
        clip = lambda name: read(os.path.join(a.impact, name + ".wav"))
        write(os.path.join(a.out, "glove-catch.wav"), glove_catch(clip("impactPunch_heavy_002"), clip("impactSoft_medium_003")))
        write(os.path.join(a.out, "blocker-hit.wav"), blocker_hit(clip("impactWood_medium_002"), clip("impactWood_light_000")))
    if a.voice: write(os.path.join(a.out, "groan.wav"), groan(read(a.voice)))
    if a.crowd: write(os.path.join(a.out, "crowd-loop.wav"), crowd_loop(read(a.crowd)))
