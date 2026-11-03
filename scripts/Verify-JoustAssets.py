"""Validate the local delivery without touching installed game resources."""
import json, sys
from pathlib import Path
from PIL import Image
root=Path(sys.argv[1])
manifest=json.loads((root/'resources/tilesets/Jousting_Objects_16.json').read_text())
atlas=Image.open(root/'resources/tilesets/Jousting_Objects_16.png').convert('RGBA')
assert atlas.width%16==0 and atlas.height%16==0
assert atlas.getchannel('A').getextrema()==(0,255)
occupied=set()
for obj in manifest['objects']:
    assert all(obj[k]%16==0 for k in ['x','y','width','height']),obj['id']
    assert obj['x']+obj['width']<=atlas.width and obj['y']+obj['height']<=atlas.height
    assert Image.open(root/'resources/tilesets/objects'/f"{obj['id']}.png").size==(obj['width'],obj['height'])
    cells={(x,y) for x in range(obj['tileX'],obj['tileX']+obj['tileWidth'])
                 for y in range(obj['tileY'],obj['tileY']+obj['tileHeight'])}
    assert not cells & occupied, obj['id']
    occupied.update(cells)
knight=Image.open(root/'resources/images/jousting/knight-closeup.png').convert('RGBA')
assert knight.size==(512,512)
for file in ['knight-closeup.png','knight-closeup-blue.png','knight-closeup-black.png','lance-foreground.png']:
    im=Image.open(root/'resources/images/jousting'/file).convert('RGBA')
    assert set(im.getchannel('A').getdata())=={0,255},file
assert Image.open(root/'resources/images/jousting/lance-foreground.png').size==(128,128)
for y in range(4):
    for x in range(4):
        cell=knight.crop((x*128,y*128,(x+1)*128,(y+1)*128))
        bbox=cell.getchannel('A').getbbox()
        assert bbox and bbox[0]>=4 and bbox[1]>=0 and bbox[2]<=124 and bbox[3]<=120
print('PASS: 16 native objects, 16px alignment, no overlapping slots, RGBA and 16 knight frames with anchors.')
