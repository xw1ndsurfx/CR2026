"""Original synthesized HQ score and Foley; no samples or external recordings.
Run with numpy and soundfile installed. Output: PCM16 WAV effects, Vorbis music.
"""
from pathlib import Path
import argparse
import json
import numpy as np
import soundfile as sf

SR = 44100
RNG = np.random.default_rng(277)

def tone(frequency, duration, decay=1.8, brightness=0.35):
    t = np.arange(round(duration * SR)) / SR
    attack = np.minimum(1, t / 0.008)
    release = np.minimum(1, (duration - t) / 0.06)
    wave = np.zeros(len(t))
    for harmonic in range(1, 7):
        wave += np.sin(2 * np.pi * frequency * harmonic * t) * brightness ** (harmonic - 1) * np.exp(-t * decay * (1 + harmonic * 0.15))
    return wave * attack * release

def noise(duration, smooth=80):
    raw = RNG.normal(0, 1, round(duration * SR))
    return np.convolve(raw, np.ones(smooth) / smooth, mode='same')

def add(target, source, time, gain=1, circular=False):
    offset = round(time * SR)
    if circular:
        np.add.at(target, (np.arange(len(source)) + offset) % len(target), source * gain)
    else:
        length = min(len(source), len(target) - offset)
        if length > 0: target[offset:offset+length] += source[:length] * gain

def finish(data, peak=0.65, loop=False):
    data -= np.mean(data, axis=0)
    if not loop:
        edge = min(441, len(data)//4)
        data[:edge] *= np.linspace(0, 1, edge)
        data[-edge:] *= np.linspace(1, 0, edge)
    else:
        # Match values AND slope at the wrap, without a silent gap.
        edge = 2205
        bridge = np.linspace(data[-edge], data[edge], edge * 2)
        blend = np.sin(np.linspace(0, np.pi, edge * 2)) ** 2
        seam = np.concatenate((data[-edge:], data[:edge]))
        seam = seam * (1-blend) + bridge * blend
        data[-edge:], data[:edge] = seam[:edge], seam[edge:]
    data *= peak / max(peak, np.max(np.abs(data)))
    return data

def effects():
    result = {}
    for name, pitches, duration in [
        ('open', [440, 587.33], .65), ('invitation', [587.33, 880, 1174.66], 1.6),
        ('upgrade', [293.66, 349.23, 440, 587.33, 698.46, 880], 3.6),
        ('error', [196, 146.83], .65)]:
        data = np.zeros(round(duration * SR))
        for i, pitch in enumerate(pitches):
            add(data, tone(pitch, min(1.8, duration), 3 if name != 'upgrade' else 1.8), i * (.18 if name != 'open' else .12), .28)
        if name == 'upgrade':
            for pitch in [146.83, 220, 293.66]: add(data, tone(pitch, 2.4, 1.4, .15), 1.0, .22)
        if name in ('open', 'invitation'):
            paper = noise(.3, 6) * np.sin(np.linspace(0, np.pi, round(.3*SR)))**2
            add(data, paper, 0, .09)
        result[name] = finish(data, .55)
    for name in ('enter', 'leave', 'bank'):
        duration = 1.7 if name == 'enter' else 1.1
        data = np.zeros(round(duration * SR))
        # Wood cavity thump plus iron latch; a slow filtered creak for the door.
        add(data, tone(82 if name != 'bank' else 145, .65, 7, .6), .04, .3)
        add(data, tone(164 if name != 'bank' else 420, .32, 12, .3), .03, .13)
        creak_len = .9 if name == 'enter' else .45
        t = np.arange(round(creak_len*SR))/SR
        creak = np.sin(2*np.pi*(115*t + 24*t*t)) * (1+.4*np.sin(2*np.pi*9*t)) * np.sin(np.pi*t/creak_len)**2
        add(data, creak, .17, .055)
        add(data, noise(.17, 18) * np.exp(-np.arange(round(.17*SR))/SR*30), .65, .18)
        add(data, tone(70, .4, 11, .25), .68, .25 if name=='leave' else .1)
        result[name] = finish(data, .58)
    hearth = noise(24, 180) * .13
    for _ in range(140):
        duration = float(RNG.uniform(.012, .07))
        crackle = noise(duration, 5) * np.exp(-np.arange(round(duration*SR))/SR*80)
        add(hearth, crackle, float(RNG.uniform(0,24)), float(RNG.uniform(.025,.12)), circular=True)
    result['hearth'] = finish(hearth, .28, loop=True)
    return result

def score():
    duration = 48
    left = np.zeros(duration*SR); right = left.copy()
    # Original eight-bar D-Dorian phrase, 80 BPM, two repeats with a gentle countermelody.
    chords = [[146.83,220,293.66],[174.61,261.63,349.23],[130.81,196,261.63],[196,293.66,392]]
    melody = [587.33,659.25,698.46,659.25,587.33,440,523.25,587.33,
              523.25,440,392,440,587.33,659.25,440,293.66]
    for bar in range(16):
        chord = chords[(bar//2)%4]
        for beat in range(4):
            when = bar*3 + beat*.75
            pitch = chord[[0,2,1,2][beat]] * (2 if beat%2 else 1)
            note = tone(pitch, 2.2, 2.4, .32)
            add(left,note,when,.095,True);add(right,note,when+.012,.075,True)
        bass = tone(chord[0]/2,2.8,1.2,.12)
        add(left,bass,bar*3,.12,True);add(right,bass,bar*3,.12,True)
        lead = tone(melody[bar],2.5,1.5,.15)
        add(left,lead,bar*3+1.5,.06,True);add(right,lead,bar*3+1.5,.085,True)
    for data in (left,right):
        dry = data.copy()
        for delay,gain in [(.071,.18),(.139,.12),(.271,.08),(.419,.04)]: data += np.roll(dry, round(delay*SR))*gain
    return np.column_stack((finish(left,.7,True),finish(right,.7,True)))

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,default=Path(__file__).resolve().parents[2]/'Intersect.Client/resources')
    target=parser.parse_args().output
    (target/'sounds').mkdir(parents=True,exist_ok=True);(target/'music').mkdir(parents=True,exist_ok=True)
    metrics={}
    for key,data in effects().items():
        path=target/'sounds'/f'guild_hq_{key}_v1.wav';sf.write(path,data,SR,subtype='PCM_16')
        metrics[path.name]={'seconds':len(data)/SR,'peak':float(np.max(np.abs(data))),'rms':float(np.sqrt(np.mean(data**2)))}
    theme=score();path=target/'music'/'guild_hq_theme_v1.ogg';sf.write(path,theme,SR,format='OGG',subtype='VORBIS')
    metrics[path.name]={'seconds':len(theme)/SR,'peak':float(np.max(np.abs(theme))),'rms':float(np.sqrt(np.mean(theme**2)))}
    print(json.dumps(metrics,indent=2))

if __name__=='__main__':
    # Vorbis encoding needs more stack than Windows' default main thread provides.
    import threading
    threading.stack_size(16 * 1024 * 1024)
    failures = []
    def run():
        try: main()
        except BaseException as error: failures.append(error)
    worker = threading.Thread(target=run)
    worker.start()
    worker.join()
    if failures: raise failures[0]
