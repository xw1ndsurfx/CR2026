"""Validate the exact optional media referenced by the crafting board."""
from pathlib import Path
import hashlib,json,struct,wave,math,array,argparse
parser=argparse.ArgumentParser()
parser.add_argument('--output',action='append',default=[],help='Also verify all media in a built client/editor directory')
args=parser.parse_args()
root=Path(__file__).resolve().parents[2]
pngs=['royal-crafting-atlas.png','icon-tools.png','icon-scroll.png','icon-aureons.png']
cues=['ui_open','ui_close','ui_select','order_posted','order_accepted','craft_hit','order_delivered','reward_aureons','order_cancelled','ui_error']
report=[]
for name in pngs:
    path=root/'resources/gui/crafting-orders'/name
    data=path.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n',name
    w,h=struct.unpack('>II',data[16:24]);assert w>0 and h>0,name
    if name=='royal-crafting-atlas.png':assert(w,h)==(448,310)
    report.append({'file':path.relative_to(root).as_posix(),'sha256':hashlib.sha256(data).hexdigest(),'width':w,'height':h})
for cue in cues:
    path=root/'resources/sounds/crafting-orders'/(cue+'.wav')
    with wave.open(str(path)) as f:
        assert f.getnchannels()==1 and f.getsampwidth()==2 and f.getcomptype()=='NONE',cue
        duration=f.getnframes()/f.getframerate();assert 0.03<=duration<=5,cue
        samples=array.array('h',f.readframes(f.getnframes()));peak=max(abs(n) for n in samples)
        assert 0<peak<32767,cue
        rms=math.sqrt(sum(n*n for n in samples)/len(samples));assert rms>50,cue
    report.append({'file':path.relative_to(root).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'seconds':round(duration,3),'peak':peak})
dest=root/'validation-local/crafting-assets.json';dest.parent.mkdir(exist_ok=True);dest.write_text(json.dumps(report,indent=2))
print(f'PASS {len(pngs)} PNG and {len(cues)} WAV: dimensions, PCM format, duration, non-silent/non-clipped samples')
for output in args.output:
    folder=Path(output)
    if not folder.is_absolute():folder=root/folder
    for entry in report:
        installed=folder/entry['file']
        assert installed.is_file(),str(installed)
        assert hashlib.sha256(installed.read_bytes()).hexdigest()==entry['sha256'],str(installed)
    print(f'PASS {len(report)} matching media files in {folder}')
