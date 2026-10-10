"""Cut the stride sounds and the hockey stop (#41) out of the #35 skating recording.

Run: py -3 Tools/cut_stride_sounds.py <skating.wav> Assets/Resources/Sounds

<skating.wav> is the 5.4 s skating cut from #35 (YleArkisto, Freesound 262256, CC BY 4.0; 77.9 to 83.3 s of the
recording). Get it back from git with:
  git show ac3fc4c:Assets/Resources/Sounds/skating.wav > skating.wav
Writes stride-1.wav .. stride-4.wav (one blade push each) and hockey-stop.wav (the loud part of the braking stop).
"""
import sys
import wave

import numpy as np

# (name, start s, end s) in skating.wav. Each stride starts just before the push's scrape swells.
STRIDES = [("stride-1", 0.28, 0.63), ("stride-2", 0.86, 1.21), ("stride-3", 1.86, 2.21), ("stride-4", 2.64, 2.99)]
STOP = ("hockey-stop", 4.55, 5.15)  # 0.6 s: RoundSoundCues.HockeyStopSeconds

STRIDE_RMS_DB = -20.0
PEAK_DB = -3.0


def read(path):
    with wave.open(path) as w:
        rate, channels = w.getframerate(), w.getnchannels()
        data = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(float) / 32768
    return rate, data.reshape(-1, channels)


def write(path, rate, samples):
    with wave.open(path, "wb") as w:
        w.setnchannels(samples.shape[1])
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes((np.clip(samples, -1, 1) * 32767).astype("<i2").tobytes())


def cut(rate, data, start, end, fade_in, fade_out):
    s = data[int(start * rate):int(end * rate)].copy()
    n_in, n_out = int(fade_in * rate), int(fade_out * rate)
    s[:n_in] *= np.linspace(0, 1, n_in)[:, None]
    s[-n_out:] *= np.linspace(1, 0, n_out)[:, None]
    return s


def level(s, rms_db):
    """Gain to rms_db RMS, lowered if the peak would pass PEAK_DB."""
    rms = np.sqrt((s ** 2).mean())
    gain = 10 ** (rms_db / 20) / rms
    gain = min(gain, 10 ** (PEAK_DB / 20) / np.abs(s).max())
    return s * gain


def main(source, out_dir):
    rate, data = read(source)
    for name, start, end in STRIDES:
        write(f"{out_dir}/{name}.wav", rate, level(cut(rate, data, start, end, 0.008, 0.1), STRIDE_RMS_DB))
    name, start, end = STOP
    write(f"{out_dir}/{name}.wav", rate, cut(rate, data, start, end, 0.03, 0.06))  # level as in #35


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
