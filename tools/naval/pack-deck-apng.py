"""Package full PNG replay frames as APNG without changing their pixels."""
import json,pathlib,struct,sys,zlib
directory=pathlib.Path(sys.argv[1]);target=pathlib.Path(sys.argv[2])
manifest=directory/'manifest.json'
frames=[directory/name for name in json.loads(manifest.read_text(encoding='utf-8-sig'))] if manifest.exists() else sorted(directory.glob('frame-*.png'))
def chunks(path):
    data=path.read_bytes();pos=8;out=[]
    while pos<len(data):
        size=struct.unpack_from('>I',data,pos)[0];kind=data[pos+4:pos+8];payload=data[pos+8:pos+8+size]
        out.append((kind,payload));pos+=12+size
    return out
def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
first=chunks(frames[0]);ihdr=first[0][1];width,height=struct.unpack_from('>II',ihdr)
output=bytearray(b'\x89PNG\r\n\x1a\n');output+=chunk(b'IHDR',ihdr)
for kind,payload in first[1:]:
    if kind==b'IDAT':break
    if kind!=b'IEND':output+=chunk(kind,payload)
output+=chunk(b'acTL',struct.pack('>II',len(frames),0));sequence=0
for index,path in enumerate(frames):
    parts=chunks(path)
    if parts[0][1]!=ihdr:raise ValueError('All replay frames must use identical PNG headers')
    output+=chunk(b'fcTL',struct.pack('>IIIIIHHBB',sequence,width,height,0,0,1,1 if index==len(frames)-1 else 5,0,0));sequence+=1
    pixels=b''.join(payload for kind,payload in parts if kind==b'IDAT')
    if index==0:output+=chunk(b'IDAT',pixels)
    else:output+=chunk(b'fdAT',struct.pack('>I',sequence)+pixels);sequence+=1
output+=chunk(b'IEND',b'');target.write_bytes(output)
print(f'{len(frames)} frames, {width}x{height}, {len(output)} bytes: {target}')
