"""Original short harp/bell guide cues; deterministic synthesis, no external samples."""
import math
import struct
import wave
from pathlib import Path

RATE = 44100
DEST = Path(__file__).resolve().parents[1] / "resources" / "sounds"


def pluck(t, start, frequency, duration, volume):
    t -= start
    if t < 0 or t >= duration:
        return 0.0
    attack = min(1.0, t / 0.009)
    tail = (1 - t / duration) ** 2
    # Soft plucked fundamental with progressively damped bell partials.
    voice = sum(weight * math.sin(2 * math.pi * frequency * ratio * t) * math.exp(-decay * t)
                for ratio, weight, decay in [(1, 1, 2), (2, .22, 5), (3, .075, 9), (4.01, .035, 14)])
    return voice * attack * tail * volume


def save(name, duration, notes):
    samples = [sum(pluck(i / RATE, *note) for note in notes) for i in range(round(duration * RATE))]
    peak = max(abs(sample) for sample in samples)
    gain = .32 / max(1, peak)  # At least 9 dB of headroom, including overlaps.
    pcm = b"".join(struct.pack("<h", round(sample * gain * 32767)) for sample in samples)
    DEST.mkdir(parents=True, exist_ok=True)
    path = DEST / f"royal_guide_{name}_v1.wav"
    with wave.open(str(path), "wb") as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(RATE)
        audio.writeframes(pcm)
    print(f"{path.name}: {duration:.2f}s, mono PCM16 / {RATE} Hz")


if __name__ == "__main__":
    save("objective", .65, [(0, 523.25, .45, .65), (.12, 783.99, .5, .45)])
    save("step", .95, [(0, 523.25, .5, .65), (.12, 659.25, .5, .55), (.25, 783.99, .65, .5)])
    save("complete", 1.5, [(0, 392, .65, .5), (.13, 523.25, .75, .6), (.26, 659.25, .8, .5),
                            (.4, 783.99, 1, .5), (.55, 1046.5, .9, .38)])
