"""Original procedural foley for Corps Royaux. No samples or network dependencies.

Layered filtered noise, damped wood/metal resonances and soft musical punctuation.
Run with --check to validate shipped files without rewriting them.
"""
from pathlib import Path
import argparse
import array
import hashlib
import json
import math
import random
import sys
import wave

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Intersect.Client/resources/sounds/interface'
MANIFEST = ROOT / 'Documentation/Interface-Audio-Assets.json'
RATE = 44100

def create(name, duration, layers, peak=.48):
    rng = random.Random('corps-royaux-ui-v1/' + name)
    samples = [0.] * int(duration * RATE)
    for material, at, length, level, pitch in layers:
        start, count = int(at * RATE), int(length * RATE)
        low = previous = 0.
        phases = [rng.random() * math.tau for _ in range(4)]
        for j in range(min(count, len(samples) - start)):
            t, u = j / RATE, j / max(1, count - 1)
            noise = rng.uniform(-1, 1)
            low += .085 * (noise - low)
            high = noise - previous
            previous = noise
            attack = min(1., t / .003)
            tail = min(1., (1 - u) * length / .018)
            if material == 'paper':
                env = math.sin(math.pi * u) ** 1.6
                value = (low * .8 + high * .075) * (1 + .24 * math.sin(math.tau * 37 * t))
            elif material == 'leather':
                env = math.sin(math.pi * u) ** 1.3
                value = low * (1 + .5 * math.sin(math.tau * (80 * t + 170 * t * t)))
            elif material == 'wood':
                env = math.exp(-t * 36)
                value = .7 * math.sin(math.tau * pitch * t) + .22 * math.sin(math.tau * pitch * 2.71 * t) + .45 * low
            elif material == 'metal':
                env = math.exp(-t * 15)
                value = sum(math.sin(math.tau * pitch * ratio * t + phase) * weight
                            for ratio, phase, weight in zip((1, 2.76, 4.13, 5.41), phases, (.48, .22, .13, .07))) + high * .04 * math.exp(-40*t)
            else:  # warm plucked accent; never a square-wave computer beep
                env = math.exp(-t * 11)
                value = math.sin(math.tau * pitch * t) * .68 + math.sin(math.tau * pitch * 2 * t) * .14 * math.exp(-16*t)
            samples[start+j] += level * value * env * attack * tail
    maximum = max(map(abs, samples))
    samples = [value / maximum * peak * min(1., i / (RATE*.002), (len(samples)-1-i)/(RATE*.015)) for i,value in enumerate(samples)]
    pcm = array.array('h', (round(value * 32767) for value in samples))
    if sys.byteorder != 'little': pcm.byteswap()
    path = OUT / f'{name}_v1.wav'
    with wave.open(str(path), 'wb') as wav:
        wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(RATE); wav.writeframes(pcm.tobytes())
    return {'cue': name, 'file': path.relative_to(ROOT).as_posix(), 'seconds': duration,
            'peak': round(max(map(abs,samples)),6), 'rms': round(math.sqrt(sum(x*x for x in samples)/len(samples)),6),
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}

def wood(at=0, pitch=260, level=1, length=.14): return ('wood',at,length,level,pitch)
def paper(at=0, length=.23, level=.5): return ('paper',at,length,level,0)
def leather(at=0, length=.23, level=.5): return ('leather',at,length,level,0)
def metal(at=0, pitch=1100, level=.3, length=.22): return ('metal',at,length,level,pitch)
def note(at, pitch, level=.3, length=.32): return ('pluck',at,length,level,pitch)

def generate():
    OUT.mkdir(parents=True, exist_ok=True)
    cues = [
        ('hover',.07,[wood(pitch=420,length=.07)],.28),
        ('click',.12,[wood(pitch=310,length=.1),leather(length=.1,level=.18)],.43),
        ('back',.18,[wood(pitch=200),paper(length=.16,level=.2)],.43),
        ('tab',.20,[paper(length=.18),wood(.035,360,.2)],.40),
        ('toggleon',.14,[wood(pitch=380,length=.11),metal(.02,1400,.12,.1)],.43),
        ('toggleoff',.14,[wood(pitch=220,length=.11),leather(length=.12,level=.2)],.40),
        ('slider',.08,[wood(pitch=470,length=.08)],.30),
        ('select',.16,[wood(pitch=300,length=.11),metal(.025,950,.1,.12)],.40),
        ('submit',.27,[wood(),note(.035,523.25,.18,.22)],.46),
        ('menuopen',.72,[paper(length=.36,level=.23),note(.02,261.63,.4,.48),note(.11,392,.23,.45),note(.21,523.25,.17,.45)],.48),
        ('panelopen',.28,[leather(length=.22),wood(.04,220,.6)],.43),
        ('panelclose',.21,[leather(length=.18),wood(.07,170,.6)],.43),
        ('bookopen',.40,[leather(length=.16),paper(.09,.28),wood(.025,180,.24)],.48),
        ('bookclose',.30,[paper(length=.17),wood(.13,140,.7),leather(.08,.19,.3)],.48),
        ('bagopen',.33,[leather(length=.28),metal(.07,820,.10,.22)],.46),
        ('chestopen',.46,[wood(pitch=130,length=.22),leather(.04,.35,.6),metal(.12,710,.13,.3)],.50),
        ('chestclose',.33,[leather(length=.2),wood(.12,105,1,.2),metal(.14,940,.1,.17)],.50),
        ('coins',.38,[metal(.01,1560,.5),metal(.07,1820,.28),metal(.14,1270,.35)],.45),
        ('armor',.36,[leather(length=.25),metal(.02,650,.4,.28),metal(.085,960,.22,.24),wood(.02,140,.25)],.48),
        ('map',.38,[paper(length=.34),leather(.02,.27,.35),wood(.21,240,.12)],.46),
        ('social',.42,[paper(length=.19,level=.23),note(.01,392,.26,.29),note(.1,493.88,.18,.3)],.45),
        ('prompt',.42,[wood(pitch=260,level=.3),note(.025,440,.3,.36),note(.04,659.25,.10,.3)],.46),
        ('notice',.51,[note(.01,523.25,.28,.32),note(.12,659.25,.18,.35),paper(.01,.2,.1)],.46),
        ('error',.34,[wood(pitch=155),wood(.12,135,.65),leather(length=.31,level=.15)],.43),
        ('reward',.85,[metal(.01,1046.5,.20,.38),note(.01,392,.35,.4),note(.12,523.25,.3,.45),note(.24,659.25,.22,.5),note(.36,783.99,.16,.45)],.52),
        ('workshop',.39,[wood(pitch=150),metal(.06,680,.2,.3),leather(.04,.23,.23)],.48),
        ('magic',.66,[paper(length=.38,level=.14),metal(.04,1318.5,.13,.4),note(.01,392,.25,.4),note(.12,587.33,.23,.45),note(.23,783.99,.16,.4)],.48),
    ]
    entries = [create(*cue) for cue in cues]
    MANIFEST.write_text(json.dumps({'sampleRate':RATE,'format':'mono PCM16 WAV','origin':'Original procedural foley; no external samples','audio':entries},indent=2)+'\n',encoding='utf-8')
    return entries

def check(entries):
    for entry in entries:
        path = ROOT / entry['file']
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], path
        with wave.open(str(path),'rb') as wav:
            assert (wav.getnchannels(),wav.getsampwidth(),wav.getframerate()) == (1,2,RATE), path
            pcm = array.array('h',wav.readframes(wav.getnframes()))
            if sys.byteorder != 'little': pcm.byteswap()
            assert len(pcm) >= RATE*.05 and max(map(abs,pcm)) < 25000 and max(map(abs,pcm)) > 500, path
            assert abs(pcm[0]) <= 1 and abs(pcm[-1]) <= 1, path
    assert len(entries) == 27
    print(f'{len(entries)} WAV files verified: hashes, format, duration, signal, headroom and silent endpoints.')

if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');parser.add_argument('--preview',action='store_true');args=parser.parse_args()
    check(json.loads(MANIFEST.read_text(encoding='utf-8'))['audio'] if args.check else generate())
    if args.preview:
        output=ROOT/'validation-local/interface-audio-preview.wav';output.parent.mkdir(parents=True,exist_ok=True)
        with wave.open(str(output),'wb') as preview:
            preview.setnchannels(1);preview.setsampwidth(2);preview.setframerate(RATE)
            for name in ('menuopen','hover','click','tab','bookopen','bagopen','chestopen','coins','magic','reward','error'):
                with wave.open(str(OUT/f'{name}_v1.wav'),'rb') as wav: preview.writeframes(wav.readframes(wav.getnframes()))
                preview.writeframes(bytes(int(RATE*.3)*2))
        print(output)
