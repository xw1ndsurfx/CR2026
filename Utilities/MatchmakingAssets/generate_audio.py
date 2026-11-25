"""Original deterministic matchmaking cues. No downloaded samples or dependencies."""
from pathlib import Path
import importlib.util, json, hashlib, wave, array, sys

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('foley', ROOT/'Utilities/InterfaceAudioAssets/generate_audio.py')
foley = importlib.util.module_from_spec(spec)
spec.loader.exec_module(foley)
foley.OUT = ROOT/'Intersect.Client/resources/sounds/matchmaking'
MANIFEST = ROOT/'Documentation/Matchmaking-Assets.json'

def generate():
    foley.OUT.mkdir(parents=True, exist_ok=True)
    w, p, l, m, n = foley.wood, foley.paper, foley.leather, foley.metal, foley.note
    cues = [
        ('open', .62, [p(0,.33,.25),l(.04,.28,.18),n(.05,293.66,.30,.42),n(.16,440,.19,.40)], .44),
        ('queued', .48, [w(0,230,.4),n(.04,293.66,.3,.35),n(.14,369.99,.23,.30)], .43),
        ('found', 1.18, [m(.01,880,.18,.42),n(.02,293.66,.38,.50),n(.18,369.99,.33,.5),n(.34,440,.29,.53),n(.5,587.33,.24,.60)], .50),
        ('ready', .58, [w(.01,310,.3),n(.02,440,.3,.38),n(.13,587.33,.25,.40),m(.12,1174.66,.09,.30)], .46),
        ('cancelled', .48, [p(.02,.27,.2),n(.01,369.99,.28,.3),n(.14,293.66,.23,.3)], .40),
        ('depart', 1.30, [w(.01,160,.2),n(.01,293.66,.37,.6),n(.16,440,.26,.65),n(.32,587.33,.22,.7),m(.38,1174.66,.12,.60)], .50),
        ('error', .42, [w(.01,175,.65,.18),w(.17,145,.45,.18),l(.03,.32,.12)], .41),
    ]
    audio = [foley.create(*cue) for cue in cues]
    images = []
    for name in ('matchmaking_background_v1.png','matchmaking_icons_v1.png'):
        path=ROOT/'Intersect.Client/resources/gui'/name
        images.append({'file':path.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                       'origin':'OpenAI built-in image generation; original Corps Royaux matchmaking UI artwork'})
    MANIFEST.write_text(json.dumps({'version':1,'origin':'Original procedural foley and generated artwork; no external samples',
                                  'audio':audio,'images':images},indent=2)+'\n',encoding='utf-8')

def check():
    data=json.loads(MANIFEST.read_text(encoding='utf-8'))
    assert len(data['audio'])==7 and len(data['images'])==2
    for entry in data['audio']+data['images']:
        assert hashlib.sha256((ROOT/entry['file']).read_bytes()).hexdigest()==entry['sha256']
    for entry in data['audio']:
        with wave.open(str(ROOT/entry['file']),'rb') as wav:
            assert (wav.getnchannels(),wav.getsampwidth(),wav.getframerate())==(1,2,44100)
            pcm=array.array('h',wav.readframes(wav.getnframes()))
            if sys.byteorder!='little':pcm.byteswap()
            assert 1000<max(map(abs,pcm))<20000 and abs(pcm[0])<=1 and abs(pcm[-1])<=1
            assert .3*44100<=len(pcm)<=1.5*44100
    print('Verified 7 original WAV cues and 2 images: hashes, format, duration, headroom and silent endpoints.')

if __name__=='__main__':
    if '--check' not in sys.argv:generate()
    check()
