#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  synth_engine.py
#  DECRYPTED — A Walk Through the History of Secret Writing
#
#  A small, dependency-light digital-audio synthesis toolkit used to generate
#  EVERY sound in the museum from scratch (no sampled or downloaded audio). It
#  provides the primitives a sound designer needs — band-limited-ish oscillators,
#  ADSR envelopes, biquad filters, a Schroeder reverb, saturation, and a 16-bit
#  PCM WAV writer built on the Python standard library so only NumPy is required.
#
#  Everything is plain NumPy so it runs anywhere Python + NumPy is installed, and
#  it is written to be read: each block is a clear, named transformation rather
#  than a clever one-liner. generate_all_audio.py composes these primitives into
#  the project's SFX and ambient loops.
# -----------------------------------------------------------------------------

import math
import struct
import wave
import numpy as np

SAMPLE_RATE = 48000          # matches the Unity runtime AudioSynth fallback
TWO_PI = 2.0 * math.pi


# ----------------------------------------------------------------- time / utils

def t_axis(duration, sr=SAMPLE_RATE):
    """Return a time vector [0, duration) at the given sample rate."""
    n = int(round(duration * sr))
    return np.linspace(0.0, duration, n, endpoint=False)


def silence(duration, sr=SAMPLE_RATE):
    return np.zeros(int(round(duration * sr)), dtype=np.float64)


def normalize(x, peak=0.97):
    """Scale so the absolute peak hits `peak`. Safe on all-zero input."""
    m = np.max(np.abs(x)) if x.size else 0.0
    return x * (peak / m) if m > 1e-9 else x


def mix(*signals):
    """Sum signals of possibly different lengths (zero-padded to the longest)."""
    n = max((s.size for s in signals), default=0)
    out = np.zeros(n, dtype=np.float64)
    for s in signals:
        out[:s.size] += s
    return out


def pad_to(x, duration, sr=SAMPLE_RATE):
    n = int(round(duration * sr))
    if x.size >= n:
        return x[:n]
    return np.concatenate([x, np.zeros(n - x.size, dtype=np.float64)])


# --------------------------------------------------------------- oscillators

def sine(freq, duration, phase=0.0, sr=SAMPLE_RATE):
    t = t_axis(duration, sr)
    return np.sin(TWO_PI * freq * t + phase)


def saw(freq, duration, sr=SAMPLE_RATE):
    """Naive sawtooth (slightly soft, which suits a Quest mix and avoids harsh aliasing)."""
    t = t_axis(duration, sr)
    return 2.0 * (t * freq - np.floor(0.5 + t * freq))


def square(freq, duration, duty=0.5, sr=SAMPLE_RATE):
    t = t_axis(duration, sr)
    frac = (t * freq) % 1.0
    return np.where(frac < duty, 1.0, -1.0)


def triangle(freq, duration, sr=SAMPLE_RATE):
    return 2.0 * np.abs(saw(freq, duration, sr)) - 1.0


def noise(duration, sr=SAMPLE_RATE, seed=None):
    rng = np.random.default_rng(seed)
    return rng.uniform(-1.0, 1.0, int(round(duration * sr)))


def chirp(f0, f1, duration, sr=SAMPLE_RATE):
    """Linear frequency sweep from f0 to f1 (phase-correct via integrated freq)."""
    t = t_axis(duration, sr)
    k = (f1 - f0) / max(duration, 1e-9)
    phase = TWO_PI * (f0 * t + 0.5 * k * t * t)
    return np.sin(phase)


# ----------------------------------------------------------------- envelopes

def adsr(duration, attack, decay, sustain, release, sr=SAMPLE_RATE):
    """Classic ADSR. attack/decay/release in seconds, sustain in [0,1]."""
    n = int(round(duration * sr))
    env = np.zeros(n, dtype=np.float64)
    a = int(attack * sr)
    d = int(decay * sr)
    r = int(release * sr)
    s = max(0, n - a - d - r)

    idx = 0
    if a > 0:
        env[idx:idx + a] = np.linspace(0.0, 1.0, a, endpoint=False); idx += a
    if d > 0:
        env[idx:idx + d] = np.linspace(1.0, sustain, d, endpoint=False); idx += d
    if s > 0:
        env[idx:idx + s] = sustain; idx += s
    if r > 0:
        env[idx:idx + r] = np.linspace(sustain, 0.0, r, endpoint=False); idx += r
    if idx < n:
        env[idx:] = 0.0
    return env


def perc_env(duration, attack=0.002, sr=SAMPLE_RATE):
    """Fast-attack exponential decay — ideal for clicks/percussive transients."""
    n = int(round(duration * sr))
    a = max(1, int(attack * sr))
    env = np.ones(n, dtype=np.float64)
    env[:a] = np.linspace(0.0, 1.0, a, endpoint=False)
    decay = np.exp(-np.linspace(0.0, 6.0, n - a))
    env[a:] = decay
    return env


def fade(x, fade_in=0.005, fade_out=0.02, sr=SAMPLE_RATE):
    y = x.copy()
    ni = int(fade_in * sr)
    no = int(fade_out * sr)
    if ni > 0:
        y[:ni] *= np.linspace(0.0, 1.0, ni)
    if no > 0:
        y[-no:] *= np.linspace(1.0, 0.0, no)
    return y


# ------------------------------------------------------------------- filters

def biquad(x, b0, b1, b2, a1, a2):
    """Direct-Form-I biquad. Coeffs already normalised by a0."""
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(x.size):
        xi = x[i]
        yi = b0 * xi + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, xi
        y2, y1 = y1, yi
        y[i] = yi
    return y


def _rbj_coeffs(kind, freq, q, sr=SAMPLE_RATE):
    w0 = TWO_PI * freq / sr
    cw, sw = math.cos(w0), math.sin(w0)
    alpha = sw / (2.0 * q)
    if kind == "lowpass":
        b0 = (1 - cw) / 2; b1 = 1 - cw; b2 = (1 - cw) / 2
    elif kind == "highpass":
        b0 = (1 + cw) / 2; b1 = -(1 + cw); b2 = (1 + cw) / 2
    elif kind == "bandpass":
        b0 = alpha; b1 = 0.0; b2 = -alpha
    else:
        raise ValueError(kind)
    a0 = 1 + alpha; a1 = -2 * cw; a2 = 1 - alpha
    return b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0


def lowpass(x, freq, q=0.707, sr=SAMPLE_RATE):
    return biquad(x, *_rbj_coeffs("lowpass", freq, q, sr))


def highpass(x, freq, q=0.707, sr=SAMPLE_RATE):
    return biquad(x, *_rbj_coeffs("highpass", freq, q, sr))


def bandpass(x, freq, q=2.0, sr=SAMPLE_RATE):
    return biquad(x, *_rbj_coeffs("bandpass", freq, q, sr))


def one_pole_lp(x, coeff=0.05):
    """Cheap smoothing one-pole (used for slow control-rate shaping)."""
    y = np.zeros_like(x)
    acc = 0.0
    for i in range(x.size):
        acc += coeff * (x[i] - acc)
        y[i] = acc
    return y


# --------------------------------------------------------------- saturation

def soft_clip(x, drive=1.5):
    """Tanh soft saturation — adds warmth/body without harsh digital clipping."""
    return np.tanh(x * drive)


# ------------------------------------------------------------------- reverb

def _comb(x, delay_ms, feedback, sr=SAMPLE_RATE):
    d = max(1, int(delay_ms * 0.001 * sr))
    y = np.zeros(x.size + d, dtype=np.float64)
    y[:x.size] = x
    for i in range(x.size):
        y[i + d] += feedback * y[i]
    return y[:x.size]


def _allpass(x, delay_ms, gain=0.5, sr=SAMPLE_RATE):
    d = max(1, int(delay_ms * 0.001 * sr))
    y = np.zeros(x.size + d, dtype=np.float64)
    buf = np.zeros(x.size + d, dtype=np.float64)
    buf[:x.size] = x
    for i in range(x.size):
        delayed = y[i]
        y[i + d] = buf[i] + (-gain) * delayed + gain * y[i]
        y[i] = -gain * buf[i] + delayed
    return y[:x.size]


def reverb(x, mix_wet=0.3, room=1.0, sr=SAMPLE_RATE):
    """A compact Schroeder reverb: parallel combs + series allpasses. `room`
    scales the comb delays/feedback for small (0.5) to large (1.5) spaces."""
    combs = [
        (29.7 * room, 0.78), (37.1 * room, 0.80),
        (41.1 * room, 0.82), (43.7 * room, 0.84),
    ]
    wet = np.zeros_like(x)
    for ms, fb in combs:
        wet += _comb(x, ms, min(fb, 0.94), sr)
    wet /= len(combs)
    wet = _allpass(wet, 5.0, 0.7, sr)
    wet = _allpass(wet, 1.7, 0.7, sr)
    return (1.0 - mix_wet) * x + mix_wet * wet


# --------------------------------------------------------------- loop helper

def make_seamless(x, xfade=0.5, sr=SAMPLE_RATE):
    """Crossfade the tail of a buffer into its head so it loops without a seam.
    Returns a buffer shortened by `xfade` seconds that tiles cleanly."""
    nf = int(xfade * sr)
    if nf <= 0 or nf * 2 >= x.size:
        return x
    head = x[:nf].copy()
    tail = x[-nf:].copy()
    ramp = np.linspace(0.0, 1.0, nf)
    blended = head * ramp + tail * (1.0 - ramp)
    out = x[:-nf].copy()
    out[:nf] = blended
    return out


# ----------------------------------------------------------------- WAV I/O

def write_wav(path, samples, sr=SAMPLE_RATE):
    """Write mono float samples in [-1,1] to a 16-bit PCM WAV (stdlib only)."""
    s = np.clip(samples, -1.0, 1.0)
    pcm = (s * 32767.0).astype(np.int16)
    with wave.open(path, "w") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sr)
        wf.writeframes(struct.pack("<%dh" % pcm.size, *pcm.tolist()))
    return path


# ----------------------------------------------------------- musical helpers

def note_freq(midi):
    """MIDI note number -> frequency (A4 = 69 = 440 Hz)."""
    return 440.0 * (2.0 ** ((midi - 69) / 12.0))


def chord(midis, duration, osc=triangle, detune=0.0, sr=SAMPLE_RATE):
    """Sum a set of MIDI notes with optional slight detune for warmth."""
    out = silence(duration, sr)
    for m in midis:
        f = note_freq(m) * (1.0 + detune)
        out = mix(out, osc(f, duration, sr=sr))
    return out / max(1, len(midis))


if __name__ == "__main__":
    # Tiny self-test: render a 440 Hz note with an ADSR and write it out.
    tone = sine(440.0, 1.0) * adsr(1.0, 0.01, 0.1, 0.7, 0.2)
    write_wav("synth_engine_selftest.wav", normalize(tone))
    print("Wrote synth_engine_selftest.wav")
