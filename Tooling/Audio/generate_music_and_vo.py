#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  generate_music_and_vo.py
#  DECRYPTED - A Walk Through the History of Secret Writing
#
#  Bakes the per-room MUSIC loops and the per-room narration (VO) placeholders,
#  plus a clean ui_confirm button click - all from synthesis (no samples), using
#  the same synth_engine toolkit as generate_all_audio.py.
#
#  Music is composed only from vectorised oscillator+envelope notes + make_seamless
#  so the loops tile cleanly and generation stays fast (heavy per-sample biquad /
#  reverb loops are avoided on long buffers).
#
#  Output (filenames match the Unity AudioManager keys):
#    Assets/_Project/Audio/Music/  mus_*.wav   (seamless loops -> import as Vorbis)
#    Assets/_Project/Audio/VO/     vo_*.wav    (silent placeholders, real length)
#    Assets/_Project/Audio/SFX/    ui_confirm.wav
#
#  Run:  py generate_music_and_vo.py
# -----------------------------------------------------------------------------

import os
import numpy as np
import synth_engine as se

SR = se.SAMPLE_RATE
HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
MUSIC_DIR = os.path.join(PROJECT, "Assets", "_Project", "Audio", "Music")
VO_DIR = os.path.join(PROJECT, "Assets", "_Project", "Audio", "VO")
SFX_DIR = os.path.join(PROJECT, "Assets", "_Project", "Audio", "SFX")


# ----------------------------------------------------------------- helpers

def buf(duration):
    return se.silence(duration)

def place(dest, sig, at):
    """Mix sig into dest starting at time `at` (seconds)."""
    i = int(round(at * SR))
    n = min(sig.size, dest.size - i)
    if n > 0:
        dest[i:i + n] += sig[:n]
    return dest

def note(midi, dur, osc=se.triangle, amp=0.5, a=0.01, d=0.1, s=0.7, r=0.2):
    # Clamp A+D+R to fit the note (synth_engine.adsr assumes a+d+r <= dur).
    tot = a + d + r
    if tot > dur * 0.98:
        k = dur * 0.98 / tot
        a, d, r = a * k, d * k, r * k
    f = se.note_freq(midi)
    return osc(f, dur) * se.adsr(dur, a, d, s, r) * amp

def pad(midis, dur, osc=se.triangle, amp=0.4, a=0.6, d=0.4, s=0.85, r=0.6):
    out = buf(dur)
    for m in midis:
        out += note(m, dur, osc, amp / max(1, len(midis)), a, d, s, r)
    return out

def bell(midi, dur, amp=0.4):
    """A soft inharmonic bell: fundamental + a couple of partials, fast attack."""
    f = se.note_freq(midi)
    env = se.adsr(dur, 0.003, dur * 0.5, 0.18, dur * 0.45)
    o = (se.sine(f, dur) + 0.5 * se.sine(f * 2.01, dur) + 0.25 * se.sine(f * 3.02, dur))
    return o * env * amp

def tick(at_dur=0.04, amp=0.2, freq=1600):
    return se.bandpass(se.noise(at_dur, seed=7), freq, q=2.0) * se.perc_env(at_dur, 0.0006) * amp

def drum(dur=0.12, amp=0.5):
    body = se.sine(70, dur) * se.perc_env(dur, 0.001) * amp
    return body

def finish(x, peak=0.62, xfade=0.5):
    return se.normalize(se.make_seamless(x, xfade), peak)


# ----------------------------------------------------------------- MUSIC

def mus_splash():
    """Sparse, curious, slightly mysterious. A few sustained bell tones, quiet."""
    dur = 10.0
    out = buf(dur)
    drone = pad([45, 52], dur, se.sine, amp=0.18, a=1.5, d=1.0, s=0.7, r=2.0)  # A2/E3
    out += drone
    # scattered bells on an open, questioning scale (A minor add9-ish)
    for at, m in [(0.4, 69), (2.6, 72), (4.2, 76), (5.0, 71), (7.3, 74), (8.6, 69)]:
        place(out, bell(m, 2.4, amp=0.34), at)
    return finish(out, 0.5)

def mus_atrium():
    """Stately, architectural, museum-warm. Slow organ/string chords."""
    dur = 12.0
    out = buf(dur)
    # warm major progression: I - vi - IV - V  (C, Am, F, G)
    chords = [(0.0, [48, 55, 60, 64]), (3.0, [45, 52, 57, 60]),
              (6.0, [41, 53, 57, 60]), (9.0, [43, 55, 59, 62])]
    for at, ch in chords:
        place(out, pad(ch, 3.2, se.saw, amp=0.30, a=0.5, d=0.6, s=0.8, r=0.7), at)
        place(out, pad(ch, 3.2, se.triangle, amp=0.18, a=0.5, d=0.6, s=0.8, r=0.7), at)
    return finish(out, 0.5)

def mus_ancient():
    """Ancient Mediterranean. Modal (Phrygian) lute melody, hand-drum, dry."""
    dur = 10.0
    out = buf(dur)
    drone = pad([40, 47], dur, se.saw, amp=0.16, a=1.0, d=1.0, s=0.7, r=1.5)  # E2/B2
    out += drone
    # E Phrygian melody fragments on a plucked tone
    mel = [(0.0, 64), (0.5, 65), (1.0, 67), (1.6, 65), (2.2, 64),
           (3.4, 60), (3.9, 62), (4.4, 64), (5.4, 64), (5.9, 65), (6.6, 64),
           (7.6, 67), (8.1, 65), (8.6, 64)]
    for at, m in mel:
        place(out, note(m, 0.55, se.triangle, amp=0.34, a=0.005, d=0.2, s=0.25, r=0.3), at)
    # sparse frame-drum
    for at in [0.0, 1.5, 3.0, 3.75, 5.0, 6.5, 8.0, 8.75]:
        place(out, drum(0.14, 0.42), at)
    return finish(out, 0.55)

def mus_wwii():
    """Tense, mechanical, wartime urgency. Low strings, ticking, restrained minor."""
    dur = 9.0
    out = buf(dur)
    # low D minor pedal + a tense minor-second neighbour
    out += pad([38, 50], dur, se.saw, amp=0.18, a=0.8, d=0.8, s=0.75, r=1.0)
    place(out, pad([41, 53], 4.0, se.saw, amp=0.14, a=0.6, d=0.6, s=0.7, r=0.8), 4.5)
    # relentless ticking grid (16th-ish), slightly mechanical
    step = 0.30
    t = 0.0
    i = 0
    while t < dur - 0.1:
        place(out, tick(0.04, 0.16 if i % 4 else 0.26, 1500), t)
        t += step
        i += 1
    return finish(out, 0.52)

def mus_vault():
    """Modern, electronic, clinical. Subtle synth pulses, digital, cool minor."""
    dur = 8.0
    out = buf(dur)
    out += pad([36, 48], dur, se.sine, amp=0.16, a=0.6, d=0.6, s=0.7, r=1.0)  # C minor-ish low
    # gated arpeggio pulse (C Eb G Bb) on a square synth
    arp = [48, 51, 55, 58, 55, 51]
    step = 0.25
    for k in range(int(dur / step)):
        m = arp[k % len(arp)]
        place(out, note(m, 0.22, se.square, amp=0.14, a=0.002, d=0.06, s=0.0, r=0.04), k * step)
    # cool digital shimmer
    for at, m in [(1.0, 84), (3.0, 87), (5.0, 84), (7.0, 86)]:
        place(out, note(m, 0.6, se.sine, amp=0.10, a=0.01, d=0.3, s=0.1, r=0.3), at)
    return finish(out, 0.5)

def mus_reveal():
    """Synthesis: opens sparse/ancient, adds mechanical mid-way, closes digital."""
    dur = 16.0
    out = buf(dur)
    # ancient open (0-6): bare modal pad + a bell
    out += pad([40, 47], dur, se.saw, amp=0.10, a=2.0, d=1.0, s=0.6, r=2.0)
    place(out, bell(64, 3.0, 0.30), 0.6)
    place(out, bell(67, 3.0, 0.26), 3.2)
    # mechanical middle (6-11): ticking + a clockwork minor figure
    t = 6.0
    while t < 11.0:
        place(out, tick(0.04, 0.16, 1400), t)
        t += 0.32
    for at, m in [(6.5, 60), (7.2, 63), (7.9, 67), (8.6, 63), (9.4, 60), (10.2, 67)]:
        place(out, note(m, 0.5, se.triangle, 0.24, 0.005, 0.2, 0.2, 0.3), at)
    # digital swell close (11-16): rising synth chord
    place(out, pad([48, 55, 60, 64, 67], 5.0, se.saw, amp=0.30, a=2.5, d=0.5, s=0.9, r=2.0), 11.0)
    place(out, note(84, 5.0, se.sine, 0.12, 1.5, 0.5, 0.4, 2.0), 11.0)
    return finish(out, 0.55, xfade=0.8)

def mus_complete():
    """Warm, resolved, celebratory but understated. Full satisfying major."""
    dur = 12.0
    out = buf(dur)
    # IV - V - I - vi resolving warmly (F, G, C, Am) then back
    chords = [(0.0, [41, 53, 57, 60, 65]), (3.0, [43, 55, 59, 62, 67]),
              (6.0, [48, 55, 60, 64, 72]), (9.0, [45, 52, 57, 60, 64])]
    for at, ch in chords:
        place(out, pad(ch, 3.2, se.saw, amp=0.26, a=0.4, d=0.6, s=0.85, r=0.8), at)
        place(out, pad(ch, 3.2, se.triangle, amp=0.16, a=0.4, d=0.6, s=0.85, r=0.8), at)
    # a gentle resolving bell melody on top
    for at, m in [(0.5, 72), (3.5, 74), (6.5, 76), (9.5, 72)]:
        place(out, bell(m, 2.4, 0.26), at)
    return finish(out, 0.55)


MUSIC = {
    "mus_splash": mus_splash, "mus_atrium": mus_atrium, "mus_ancient": mus_ancient,
    "mus_wwii": mus_wwii, "mus_vault": mus_vault, "mus_reveal": mus_reveal,
    "mus_complete": mus_complete,
}

# ----------------------------------------------------------------- VO placeholders
# Real lines live in RoomNarratorController.cs (captions). These are silent WAVs of
# realistic length so the system is fully wired; drop real recordings in by key.
VO_SECONDS = {
    "vo_splash": 8.5, "vo_atrium": 7.5, "vo_ancient": 9.0, "vo_wwii": 8.5,
    "vo_vault": 9.5, "vo_reveal": 7.5, "vo_complete": 7.0,
}

def ui_confirm():
    """A short, clean, satisfying button confirm: soft click + a brief bright ping."""
    click = se.bandpass(se.noise(0.03, seed=42), 2200, q=2.5) * se.perc_env(0.03, 0.0006) * 0.5
    ping = se.mix(se.sine(1320, 0.18), se.sine(1980, 0.18) * 0.3) * se.adsr(0.18, 0.002, 0.06, 0.2, 0.1) * 0.7
    out = se.mix(click, ping)
    return se.fade(se.normalize(out, 0.85), 0.001, 0.04)


def main():
    for d in (MUSIC_DIR, VO_DIR, SFX_DIR):
        os.makedirs(d, exist_ok=True)

    print("Baking room music loops...")
    for key, fn in MUSIC.items():
        se.write_wav(os.path.join(MUSIC_DIR, key + ".wav"), fn())
        print(f"  ok {key}.wav")

    print("Baking VO placeholders (silent, real length)...")
    for key, secs in VO_SECONDS.items():
        se.write_wav(os.path.join(VO_DIR, key + ".wav"), se.silence(secs))
        print(f"  ok {key}.wav ({secs:.1f}s)")

    print("Baking ui_confirm...")
    se.write_wav(os.path.join(SFX_DIR, "ui_confirm.wav"), ui_confirm())
    print("  ok ui_confirm.wav")

    print("\nDone. Refresh Unity; assign these by key in the AudioManager library.")


if __name__ == "__main__":
    main()
