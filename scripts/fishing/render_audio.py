#!/usr/bin/env python3
"""Original procedural fishing SFX. Requires Python 3, NumPy and SciPy."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import wave

import numpy as np
from scipy import signal

SR = 44100
SEED = 20261102


def rng_for(name):
    return np.random.default_rng(SEED + int(hashlib.sha256(name.encode()).hexdigest()[:12], 16))


def size(seconds):
    return int(round(seconds * SR))


def fade(x, attack=0.006, release=0.025):
    y = np.asarray(x, dtype=np.float64).copy()
    a, b = min(size(attack), len(y)//2), min(size(release), len(y)//2)
    if a:
        w = np.sin(np.linspace(0, np.pi/2, a))**2
        y[:a] *= w if y.ndim == 1 else w[:, None]
    if b:
        w = np.cos(np.linspace(0, np.pi/2, b))**2
        y[-b:] *= w if y.ndim == 1 else w[:, None]
    return y


def tone(freq, duration, end=None, decay=5.0):
    t = np.arange(size(duration)) / SR
    end = freq if end is None else end
    phase = 2*np.pi*(freq*t + 0.5*(end-freq)/duration*t*t)
    return fade(np.sin(phase)*np.exp(-decay*t/duration), .003, .02)


def noise(rng, duration, low=100, high=5000):
    x = rng.normal(0, 1, size(duration))
    sos = signal.butter(3, [low, high], fs=SR, btype='bandpass', output='sos')
    return signal.sosfilt(sos, x)


def add(dst, src, at=0.0, gain=1.0):
    start = size(at)
    n = min(len(src), len(dst)-start)
    if n > 0:
        dst[start:start+n] += src[:n] * gain


def bell(freq, duration=.35):
    t = np.arange(size(duration))/SR
    y = np.zeros(len(t))
    for ratio, gain, tau in [(1,1,.28),(2.01,.28,.17),(3.98,.10,.09)]:
        y += gain*np.sin(2*np.pi*freq*ratio*t)*np.exp(-t/tau)
    return fade(y,.003,.03)


def chord(notes, duration=.7, stagger=.07):
    y=np.zeros(size(duration))
    for i,f in enumerate(notes):
        add(y,bell(f,max(.1,duration-i*stagger)),i*stagger,.7/len(notes))
    return y


def bubbles(rng, duration, count=10):
    y=np.zeros(size(duration))
    for i in range(count):
        at=float(rng.uniform(0,max(.001,duration-.1)))
        dur=float(rng.uniform(.055,.15))
        freq=float(rng.uniform(350,1500))
        add(y,tone(freq,dur,freq*.55,4),at,float(rng.uniform(.12,.4)))
    return y


def splash(rng,duration=.65):
    t=np.arange(size(duration))/SR
    env=(1-np.exp(-t/.012))*np.exp(-t/.12)
    y=noise(rng,duration,180,6500)*env
    y+=.4*bubbles(rng,duration,12)
    return fade(y,.003,.04)


def swish(rng,duration=.6,cloth=False):
    t=np.arange(size(duration))/SR
    y=noise(rng,duration,250 if cloth else 500,3400 if cloth else 7200)
    env=np.sin(np.pi*np.minimum(t/duration,1))**2
    return fade(y*env,.01,.025)


def knock(rng,freq=180,duration=.16):
    t=np.arange(size(duration))/SR
    return fade((np.sin(2*np.pi*freq*t)+.3*noise(rng,duration,400,4500))*np.exp(-t/.025),.001,.02)


def stitch_loop(raw, overlap_seconds=.18):
    """Crossfade the extra tail into the head, then rotate to a quiet seam."""
    overlap=size(overlap_seconds)
    n=len(raw)-overlap
    w=.5-.5*np.cos(np.linspace(0,np.pi,overlap))
    if raw.ndim>1:
        w=w[:,None]
    mixed=raw[n:n+overlap]*(1-w)+raw[:overlap]*w
    y=np.concatenate([raw[overlap:n],mixed])
    y-=y.mean(axis=0)
    # The join is an adjacent sample pair from the original continuous head.
    # Choosing a quiet, low-slope pair further suppresses loop boundary steps.
    delta=np.abs(y-np.roll(y,1,axis=0))
    level=np.abs(y)
    if y.ndim>1:
        delta=delta.max(axis=1)
        level=level.max(axis=1)
    index=int(np.argmin(delta+.01*level))
    return np.roll(y,-index,axis=0)


def reel_loop(rng,duration=2.0):
    total=duration+.18
    y=np.zeros(size(total))
    click=knock(rng,760,.036)
    for at in np.arange(0,total,1/14):
        add(y,click,float(at),.45)
    t=np.arange(len(y))/SR
    y+=noise(rng,total,350,1700)*(.035+.01*np.sin(2*np.pi*2*t))
    return stitch_loop(y)


def tension_loop(rng,duration=2.4):
    total=duration+.18
    t=np.arange(size(total))/SR
    phase=2*np.pi*(170*t+2*np.sin(2*np.pi*1.25*t))
    y=(np.sin(phase)+.18*np.sin(phase*2.01))*(.3+.07*np.sin(2*np.pi*2.5*t))
    y+=noise(rng,total,220,1300)*.08
    return stitch_loop(y)


def sea_loop(rng,duration=12.0):
    total=duration+.75
    t=np.arange(size(total))/SR
    shared=noise(rng,total,90,1800)
    result=[]
    for offset in [0,.7]:
        wash=noise(rng,total,120,4200)
        swell=.5+.32*np.sin(2*np.pi*t/5.4+offset)+.09*np.sin(2*np.pi*t/2.8+offset)
        result.append((.62*wash+.38*shared)*swell)
    return stitch_loop(np.stack(result,axis=1),.75)


def render(name):
    r=rng_for(name)
    if name=='ui_open': return chord([392,587.33],.34,.065)
    if name=='ui_close': return chord([587.33,392],.28,.055)
    if name=='ui_select': return .7*knock(r,680,.11)+.2*tone(880,.11,700,8)
    if name=='ui_confirm': return chord([523.25,783.99],.42,.065)
    if name=='ui_error':
        y=np.zeros(size(.36)); add(y,tone(220,.18,196,5)); add(y,tone(196,.18,174.6,5),.14); return y
    if name=='cast_rod':
        y=np.zeros(size(.75)); add(y,swish(r,.5)); add(y,knock(r,280,.11),.12,.18); add(y,splash(r,.3),.43,.45); return y
    if name=='cast_net':
        y=np.zeros(size(.95)); add(y,swish(r,.67,True),0,.8); add(y,splash(r,.42),.5,.8); return y
    if name=='cast_cage':
        y=np.zeros(size(1.0)); add(y,knock(r,130,.22),0,.7); add(y,knock(r,310,.15),.12,.3); add(y,splash(r,.65),.32,1); return y
    if name=='splash': return splash(r,.65)
    if name=='bite':
        y=np.zeros(size(.42)); add(y,bubbles(r,.28,3)); add(y,tone(780,.13,430,5),.04,.5); add(y,splash(r,.2),.13,.3); return y
    if name=='reel_loop': return reel_loop(r)
    if name=='tension_loop': return tension_loop(r)
    if name=='line_break':
        y=np.zeros(size(.42)); add(y,knock(r,1280,.08),0,.6); add(y,tone(540,.32,140,6),.02,.55); add(y,swish(r,.19),.03,.15); return y
    if name=='escape':
        y=splash(r,.7); add(y,tone(392,.4,196,5),.15,.23); return y
    if name=='catch':
        y=np.zeros(size(.9)); add(y,splash(r,.4),0,.5); add(y,chord([392,493.88,587.33],.65,.05),.18,.8); return y
    if name=='crate_raise':
        y=np.zeros(size(1.3)); add(y,swish(r,.65,True),0,.35)
        for at in [.05,.17,.29,.41,.53]: add(y,knock(r,240,.1),at,.28)
        add(y,splash(r,.6),.5,.5); add(y,knock(r,120,.2),1.02,.7); return y
    if name=='net_retrieve':
        y=np.zeros(size(1.05)); add(y,swish(r,.6,True),0,.55); add(y,bubbles(r,.45,9),.45,.65); add(y,splash(r,.36),.58,.4); return y
    if name=='skill_stabilize': return chord([293.66,440],.5,.07)
    if name=='skill_pull':
        y=swish(r,.45); add(y,tone(260,.3,620,3),0,.5); return y
    if name=='skill_repair': return chord([440,554.37,659.25],.7,.09)
    if name=='skill_sink':
        y=bubbles(r,.65,8); add(y,tone(650,.5,170,4),0,.35); return y
    if name=='auto_start': return chord([329.63,493.88],.45,.09)
    if name=='auto_stop': return chord([493.88,329.63],.42,.09)
    if name=='auto_pause': return chord([392,392],.5,.18)
    if name=='legendary_appear':
        dur=2.1; t=np.arange(size(dur))/SR; y=np.zeros(len(t))
        for f,g in [(73.416,.22),(146.832,.16),(220,.10)]: y+=g*np.sin(2*np.pi*f*t)*np.sin(np.pi*t/dur)**2
        add(y,swish(r,1.3),0,.24); add(y,chord([293.66,440,587.33],1.25,.13),.55,.75); return y
    if name=='legendary_challenge_cue': return chord([659.25,880],.36,.055)
    if name=='legendary_challenge_success': return chord([523.25,659.25,783.99],.7,.07)
    if name=='legendary_challenge_fail':
        y=np.zeros(size(.62)); add(y,tone(311.13,.5,207.65,3)); add(y,knock(r,155,.16),0,.25); return y
    if name=='legendary_catch':
        y=np.zeros(size(2.2)); add(y,splash(r,.7),0,.45); add(y,chord([261.63,329.63,392,523.25],1.85,.14),.2,.85); add(y,bell(1046.5,.7),1.18,.13); return y
    if name=='xp_gain': return chord([659.25,783.99],.28,.045)
    if name=='level_up': return chord([392,493.88,587.33,783.99],1.45,.15)
    if name=='ambience_sea_loop': return sea_loop(r)
    raise ValueError(name)


# key, French / English / Spanish editor labels, bus, default gain, spatial, cooldown ms, concurrency.
SPECS=[
 ('ui_open','Ouvrir','Open','Abrir','ui',.45,False,80,1),
 ('ui_close','Fermer','Close','Cerrar','ui',.45,False,80,1),
 ('ui_select','Sélection','Select','Seleccionar','ui',.32,False,45,2),
 ('ui_confirm','Confirmer','Confirm','Confirmar','ui',.45,False,100,1),
 ('ui_error','Action indisponible','Unavailable action','Acción no disponible','ui',.45,False,300,1),
 ('cast_rod','Lancer la canne','Cast rod','Lanzar la caña','world',.7,True,500,2),
 ('cast_net','Lancer le filet','Cast net','Lanzar la red','world',.7,True,500,2),
 ('cast_cage','Immerger la cage','Lower cage','Sumergir la jaula','world',.7,True,500,2),
 ('splash','Éclaboussure','Splash','Salpicadura','world',.55,True,100,3),
 ('bite','Morsure','Bite','Picada','fishing',.7,False,200,1),
 ('reel_loop','Moulinet en boucle','Reel loop','Bucle del carrete','fishing',.45,False,0,1),
 ('tension_loop','Tension de ligne','Line tension','Tensión del sedal','fishing',.35,False,0,1),
 ('line_break','Ligne cassée','Line break','Rotura del sedal','fishing',.65,False,200,1),
 ('escape','Prise échappée','Catch escaped','Captura escapada','fishing',.55,False,250,1),
 ('catch','Prise réussie','Catch landed','Captura lograda','fishing',.6,False,250,1),
 ('crate_raise','Remonter la cage','Raise cage','Recoger la jaula','world',.65,True,500,1),
 ('net_retrieve','Ramener le filet','Retrieve net','Recoger la red','world',.65,True,500,1),
 ('skill_stabilize','Stabiliser','Stabilize','Estabilizar','fishing',.5,False,150,1),
 ('skill_pull','Traction','Pull','Tirar','fishing',.55,False,150,1),
 ('skill_repair','Réparer','Repair','Reparar','fishing',.5,False,150,1),
 ('skill_sink','Plonger','Sink','Hundir','fishing',.5,False,150,1),
 ('auto_start','Démarrer la pêche auto','Start auto-fishing','Iniciar pesca automática','ui',.45,False,200,1),
 ('auto_stop','Arrêter la pêche auto','Stop auto-fishing','Detener pesca automática','ui',.45,False,200,1),
 ('auto_pause','Mettre la pêche auto en pause','Pause auto-fishing','Pausar pesca automática','ui',.45,False,200,1),
 ('legendary_appear','Apparition légendaire','Legendary appearance','Aparición legendaria','fishing',.7,False,1000,1),
 ('legendary_challenge_cue','Signal de défi','Challenge cue','Señal del desafío','fishing',.6,False,100,1),
 ('legendary_challenge_success','Défi réussi','Challenge success','Desafío superado','fishing',.55,False,150,1),
 ('legendary_challenge_fail','Défi manqué','Challenge failed','Desafío fallido','fishing',.55,False,150,1),
 ('legendary_catch','Prise légendaire','Legendary catch','Captura legendaria','fishing',.75,False,800,1),
 ('xp_gain','Expérience gagnée','Experience gained','Experiencia obtenida','ui',.28,False,150,1),
 ('level_up','Niveau supérieur','Level up','Subida de nivel','ui',.65,False,1000,1),
 ('ambience_sea_loop','Ambiance de mer','Sea ambience','Ambiente marino','ambience',.6,False,0,1),
]


def peak_target(key):
    return -18 if key=='ambience_sea_loop' else -12 if key.endswith('_loop') else -6


def write_wav(path,x):
    pcm=np.round(np.clip(x,-1,1)*32767).astype('<i2')
    with wave.open(str(path),'wb') as w:
        w.setnchannels(1 if pcm.ndim==1 else pcm.shape[1])
        w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())


def inspect(path,loop):
    with wave.open(str(path),'rb') as w:
        channels, rate, width, n=w.getnchannels(),w.getframerate(),w.getsampwidth(),w.getnframes()
        pcm=np.frombuffer(w.readframes(n),dtype='<i2').reshape(-1,channels)
    x=pcm.astype(np.float64)/32768
    peak=float(np.max(np.abs(x))); rms=float(np.sqrt(np.mean(x*x)))
    delta=np.abs(np.diff(x,axis=0)); seam=np.abs(x[0]-x[-1])
    p99=np.quantile(delta,.99,axis=0)
    seam_ok=bool(np.all(seam<=np.maximum(p99,.002)) and np.max(seam)<.008)
    endpoints_ok=bool(np.all(pcm[0]==0) and np.all(pcm[-1]==0))
    result={'file':path.name,'sample_rate_hz':rate,'channels':channels,'pcm_bits':width*8,
      'duration_seconds':round(n/rate,6),'frames':n,'peak_dbfs':round(20*np.log10(max(peak,1e-12)),3),
      'rms_dbfs':round(20*np.log10(max(rms,1e-12)),3),'dc_per_channel':x.mean(axis=0).tolist(),
      'clipped_samples':int(np.sum(np.abs(pcm.astype(np.int32))>=32767)),
      'seam_step_per_channel':seam.tolist(),'adjacent_step_p99_per_channel':p99.tolist(),
      'loop_seam_ok':seam_ok if loop else None,'oneshot_endpoints_zero':endpoints_ok if not loop else None,
      'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
    result['passed']=bool(rate==SR and width==2 and channels in [1,2] and peak<.51 and result['clipped_samples']==0 and (seam_ok if loop else endpoints_ok))
    return result


def build(out):
    wavdir=out/'wav'; wavdir.mkdir(parents=True,exist_ok=True)
    assets=[]; checks=[]
    for key,fr,en,es,bus,gain,spatial,cooldown,maxvoices in SPECS:
        loop=key.endswith('_loop')
        x=render(key)
        x=x-x.mean(axis=0)
        if not loop: x=fade(x,.004,.028)
        target=10**(peak_target(key)/20)
        x=x/max(np.max(np.abs(x)),1e-12)*target
        path=wavdir/f'fishing_{key}.wav'; write_wav(path,x)
        check=inspect(path,loop); checks.append(check)
        assets.append({'id':key,'file':f'wav/{path.name}','runtime_path':f'fishing/{path.name}','label_i18n':{'fr':fr,'en':en,'es':es},
            'duration_seconds':check['duration_seconds'],'format':'PCM_S16LE_WAV','sample_rate_hz':SR,
            'channels':check['channels'],'bus':bus,'enabled':True,'gain':gain,
            'pitch_semitones':0,'pitch_random_range_semitones':[0,0] if loop else [-.12,.12],
            'loop':loop,'loop_start_frame':0 if loop else None,'loop_end_frame_exclusive':check['frames'] if loop else None,
            'fade_in_ms':100 if loop else 0,'fade_out_ms':160 if loop else 0,
            'crossfade_ms':750 if key=='ambience_sea_loop' else 180 if loop else 0,
            'crossfade_baked_into_asset':loop,'runtime_loop_crossfade_required':False,
            'cooldown_ms':cooldown,'max_concurrent_voices':maxvoices,'concurrency_group':f'fishing.{key}',
            'concurrency_policy':'replace_oldest','spatial':{'enabled':spatial,'source':'world_fishing_position' if spatial else 'listener',
                'distance_unit':'tiles','reference_distance':2 if spatial else None,'maximum_distance':12 if spatial else None,
                'rolloff':'linear' if spatial else 'none'},
            'stop_on_session_end':loop,'peak_dbfs':check['peak_dbfs'],'sha256':check['sha256']})
    events={key:{'sound_id':key,'gain_override':None,'enabled':True} for key,*_ in SPECS}
    for event,key in {
      'session_start':'ui_open','session_end':'ui_close','session_interrupted':'auto_stop',
      'tool_changed':'ui_select','tool_missing':'ui_error','tool_incompatible':'ui_error',
      'bait_missing':'ui_error','inventory_full':'ui_error','zone_unavailable':'ui_error',
      'legendary_hooked':'legendary_appear','challenge_timing_prompt':'legendary_challenge_cue',
      'challenge_direction_prompt':'legendary_challenge_cue','challenge_reel_prompt':'legendary_challenge_cue',
      'legendary_timeout':'legendary_challenge_fail','legendary_escape':'escape',
      'auto_bait_empty':'auto_pause','auto_inventory_full':'auto_pause','auto_legendary_wait':'auto_pause',
      'reward_item':'catch','reward_xp':'xp_gain','rod_cast':'cast_rod','net_cast':'cast_net','cage_cast':'cast_cage',
      'cage_retrieve':'crate_raise','line_tension_enter':'tension_loop','reeling_start':'reel_loop'
    }.items(): events[event]={'sound_id':key,'gain_override':None,'enabled':True}
    manifest={'schema':'corps-royaux.fishing-audio-pack.v1','status':'proposed integration data; adapter required for current engine',
      'provenance':'original procedural synthesis for this task','third_party_samples':False,
      'generator':'render_audio.py','seed':SEED,'language_codes':['en','fr','es'],
      'engine_copy_destination':'resources/sounds/fishing/','engine_playback_path_prefix':'fishing/',
      'defaults':{'master_gain':.8,'allow_silent_events':True,'null_sound_id_means':'silence',
        'override_order':['global','zone','equipped_tool','catch_profile','exact_event'],
        'loop_lifetime':'start once on entering state; stop on exiting state or closing/interruption'},
      'integration_notes':[
        'All sound identifiers, gains, bus, pitch, cooldown, concurrency and spatial parameters are editable.',
        'Paths are relative to this pack; rewrite with the engine sound resource resolver on import.',
        'World SFX must be emitted once; do not double-play local and replicated sound.',
        'Start loops on state transitions, not each network update; fade and stop all owned loops on cleanup.',
        'Cast assets include a soft splash. Disable the separate splash event for that same contact to avoid doubling.',
        'Catch assets include success tones. Batch XP reward sounds so catch and XP do not stack on every item.',
        'Legendary cues must accompany visible challenge prompts and never be the only gameplay signal.',
        'One sea ambience file is supplied. It is not an authored lake, river or storm ambience.'
      ],'assets':assets,'events':events,
      'stop_events':{'reeling_stop':['reel_loop'],'line_tension_exit':['tension_loop'],
        'session_end':['reel_loop','tension_loop','ambience_sea_loop'],
        'session_interrupted':['reel_loop','tension_loop','ambience_sea_loop']}}
    (out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    report={'all_passed':all(c['passed'] for c in checks),'asset_count':len(checks),
      'checks':['PCM WAV 16-bit, 44100 Hz','mono SFX; stereo ambience','peak headroom >= 6 dB','no clipped samples',
        'zero first/last sample for one-shots','loop seam step under 0.008 full scale and ordinary adjacent-step bound'],
      'auditory_review':'Not performed. Validation is numerical; final game mix and hearing review required.',
      'assets':checks}
    (out/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({'output':str(out),'assets':len(checks),'all_passed':report['all_passed'],
      'failures':[c['file'] for c in checks if not c['passed']]},indent=2))
    if not report['all_passed']: raise SystemExit(1)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out',type=Path,default=Path(__file__).resolve().parent)
    args=parser.parse_args()
    build(args.out.resolve())
