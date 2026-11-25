"""Original deterministic Royal Races cues, without downloaded audio samples."""
from pathlib import Path
import importlib.util, json, hashlib, wave, array, sys

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('foley', ROOT/'Utilities/InterfaceAudioAssets/generate_audio.py')
foley = importlib.util.module_from_spec(spec)
spec.loader.exec_module(foley)
foley.OUT = ROOT/'Intersect.Client/resources/sounds/racing'
MANIFEST = ROOT/'Documentation/Mount-Racing-Assets.json'

def generate():
    foley.OUT.mkdir(parents=True, exist_ok=True)
    w, p, l, m, n = foley.wood, foley.paper, foley.leather, foley.metal, foley.note
    cues = [
        ('open', .65, [l(0,.3,.24),p(.08,.3,.16),n(.04,329.63,.32,.40),n(.18,493.88,.24,.37)], .43),
        ('ready', .75, [w(0,260,.28),n(.03,329.63,.28,.34),n(.16,415.30,.30,.36),n(.30,493.88,.24,.34)], .46),
        ('countdown', .32, [w(.01,210,.42),m(.01,659.25,.16,.23)], .41),
        ('start', 1.25, [w(.01,180,.23),n(.01,329.63,.38,.65),n(.16,493.88,.35,.68),n(.32,659.25,.28,.72),m(.34,1318.51,.09,.6)], .50),
        ('checkpoint', .38, [n(.01,659.25,.23,.28),m(.04,1318.51,.09,.23)], .39),
        ('lap', .68, [n(.01,493.88,.3,.4),n(.16,659.25,.24,.4),m(.22,1318.51,.1,.3)], .43),
        ('finish', 1.45, [w(.01,180,.2),n(.01,329.63,.3,.65),n(.17,415.30,.3,.65),n(.33,493.88,.32,.7),n(.50,659.25,.28,.8),m(.52,1318.51,.12,.7)], .51),
        ('reward', .90, [m(.01,830.61,.13,.42),n(.03,415.30,.3,.42),n(.18,659.25,.22,.45),n(.32,830.61,.16,.47)], .46),
        ('cancel', .55, [l(.01,.3,.2),n(.02,415.30,.23,.3),n(.18,329.63,.2,.3)], .39),
    ]
    audio = [foley.create(*cue) for cue in cues]
    images = []
    for name in ('racing_background_v1.png','racing_icons_v1.png'):
        path = ROOT/'Intersect.Client/resources/gui'/name
        images.append({'file':path.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                       'origin':'OpenAI built-in image generation; original Corps Royaux Royal Races interface artwork'})
    MANIFEST.write_text(json.dumps({'version':1,'origin':'Original procedural foley and generated artwork; no external samples',
                                   'audio':audio,'images':images},indent=2)+'\n',encoding='utf-8')

def check():
    data = json.loads(MANIFEST.read_text(encoding='utf-8'))
    assert len(data['audio']) == 9 and len(data['images']) == 2
    for entry in data['audio']+data['images']:
        assert hashlib.sha256((ROOT/entry['file']).read_bytes()).hexdigest() == entry['sha256']
    for entry in data['audio']:
        with wave.open(str(ROOT/entry['file']),'rb') as wav:
            assert (wav.getnchannels(),wav.getsampwidth(),wav.getframerate()) == (1,2,44100)
            pcm = array.array('h',wav.readframes(wav.getnframes()))
            if sys.byteorder != 'little': pcm.byteswap()
            assert 1000 < max(map(abs,pcm)) < 20000 and abs(pcm[0]) <= 1 and abs(pcm[-1]) <= 1
            assert .3*44100 <= len(pcm) <= 1.5*44100
    print('Verified 9 WAV cues and 2 images: hashes, format, duration, headroom and endpoints.')

if __name__ == '__main__':
    if '--check' not in sys.argv: generate()
    check()
