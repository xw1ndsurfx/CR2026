"""Generate the original Royal Alchemist PCM effects. No external samples/dependencies."""
import math
import struct
import wave
from pathlib import Path

RATE = 44100
DEST = Path(__file__).resolve().parents[1] / "resources" / "sounds"
DEST.mkdir(parents=True, exist_ok=True)

def note(t, start, duration, frequency, volume=1, sweep=0):
    t -= start
    if t < 0 or t >= duration:
        return 0.0
    envelope = min(1, t / .008) * (1 - t / duration) ** 2
    phase = 2 * math.pi * (frequency * t + sweep * t * t / 2)
    return volume * envelope * (math.sin(phase) + .22 * math.sin(phase * 2.01))

def save(name, duration, voices):
    samples = [sum(note(i / RATE, *voice) for voice in voices) for i in range(round(duration * RATE))]
    peak = max(abs(x) for x in samples) or 1
    # Fixed comfortable headroom, avoid clipping even when the effects overlap.
    pcm = b"".join(struct.pack("<h", round(x / max(1, peak) * 18000)) for x in samples)
    with wave.open(str(DEST / ("royal_alchemist_" + name + "_v1.wav")), "wb") as sound:
        sound.setnchannels(1)
        sound.setsampwidth(2)
        sound.setframerate(RATE)
        sound.writeframes(pcm)

save("rotate", .18, [(0, .16, 1180, .35), (.025, .12, 1770, .16)])
save("drop", .34, [(0, .13, 720, .5, -1300), (.06, .22, 350, .42, -500), (.13, .16, 1420, .18)])
save("merge", .75, [(0, .20, 230, .3, 550), (.12, .24, 340, .35, 780), (.26, .26, 480, .32, 900), (.42, .28, 960, .25)])
save("complete", 1.25, [(0, .6, 523.25, .32), (.15, .6, 659.25, .3), (.3, .65, 783.99, .3), (.47, .75, 1046.5, .3)])
save("game_over", .9, [(0, .38, 392, .32), (.19, .38, 329.63, .3), (.38, .5, 261.63, .34)])
