"""Original synthesized UI/forge cues; no external samples. Run from any directory."""
from pathlib import Path
import array, hashlib, json, math, random, struct, wave

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Intersect.Client/resources/sounds"
RATE = 44100

def sound(name, seconds, notes, metal=0.0, noise=0.0):
    rng = random.Random(name)
    samples = [0.0] * int(seconds * RATE)
    for at, frequency, strength, decay in notes:
        start = int(at * RATE)
        for i in range(start, len(samples)):
            t = (i - start) / RATE
            envelope = min(1, t / .004) * math.exp(-decay * t)
            tone = math.sin(math.tau * frequency * t)
            tone += metal * (.35 * math.sin(math.tau * frequency * 2.76 * t) + .22 * math.sin(math.tau * frequency * 4.13 * t))
            samples[i] += strength * envelope * (tone + noise * rng.uniform(-1, 1) * math.exp(-35 * t))
    peak = max(abs(x) for x in samples) or 1
    samples = [x / peak * .65 * min(1, (len(samples)-i-1)/(RATE*.02)) for i, x in enumerate(samples)]
    data = array.array('h', [round(max(-1,min(1,x))*32767) for x in samples])
    file = OUTPUT / f"equipment_{name}_v1.wav"
    with wave.open(str(file), 'wb') as stream:
        stream.setnchannels(1); stream.setsampwidth(2); stream.setframerate(RATE); stream.writeframes(data.tobytes())
    return {"file": file.relative_to(ROOT).as_posix(), "seconds": seconds, "peak": round(max(map(abs,samples)),4),
            "rms": round(math.sqrt(sum(x*x for x in samples)/len(samples)),4), "sha256": hashlib.sha256(file.read_bytes()).hexdigest()}

OUTPUT.mkdir(parents=True, exist_ok=True)
audio = [
    sound('open', .40, [(0,220,.6,18),(.055,440,.3,14)], .15,.4),
    sound('select', .16, [(0,660,.3,38)], .15,.2),
    sound('save', .58, [(0,523.25,.5,12),(.10,659.25,.4,10),(.20,783.99,.35,9)], .10),
    sound('equip', .52, [(0,146.83,.7,15),(.06,880,.23,17),(.10,1320,.17,16)], .8,.65),
    sound('delete', .30, [(0,392,.4,25),(.08,261.63,.3,22)], .05,.2),
    sound('error', .38, [(0,196,.35,20),(.14,185,.3,22)], .05),
    sound('forge', .95, [(0,420,.6,12),(.32,560,.5,13),(.65,840,.4,15)], .95,.8),
    sound('upgrade', 1.20, [(0,261.63,.6,12),(.12,523.25,.5,9),(.25,659.25,.4,8),(.40,783.99,.3,7),(.55,1046.5,.2,6)], .25,.15),
    sound('spell', .75, [(0,392,.25,10),(.06,587.33,.3,9),(.12,783.99,.28,8),(.18,1174.66,.12,7)], .2),
]
images=[]
for file in sorted((ROOT/'Intersect.Client/resources/gui').glob('equipment_*_v1.png')):
    data=file.read_bytes(); width,height=struct.unpack('>II',data[16:24])
    images.append({'file':file.relative_to(ROOT).as_posix(),'width':width,'height':height,'alpha':data[25]==6,'sha256':hashlib.sha256(data).hexdigest()})
(ROOT/'Documentation/Equipment-Assets.json').write_text(json.dumps({'sampleRate':RATE,'format':'mono PCM16 WAV','audio':audio,'images':images},indent=2)+'\n',encoding='utf-8')
print(f"Validated {len(audio)} original WAV cues and {len(images)} images.")
