"""Validate the shipped HQ pack; requires numpy and soundfile for audio decoding.
PNG validation uses the standard library. --manifest records dimensions, audio
measurements and SHA-256 hashes without changing any media file.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import zlib
import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]

def png(path):
    data = path.read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', path
    position = 8
    compressed = bytearray()
    size = None
    while position < len(data):
        length = struct.unpack('>I', data[position:position+4])[0]
        kind = data[position+4:position+8]
        payload = data[position+8:position+8+length]
        crc = struct.unpack('>I', data[position+8+length:position+12+length])[0]
        assert zlib.crc32(kind + payload) == crc, (path, kind)
        if kind == b'IHDR':
            width, height, depth, color, _, _, _ = struct.unpack('>IIBBBBB', payload)
            assert 1 <= width <= 2048 and 1 <= height <= 2048 and depth == 8, path
            size = {'width': width, 'height': height, 'color_type': color}
        if kind == b'IDAT': compressed.extend(payload)
        position += length + 12
        if kind == b'IEND': break
    assert size and position == len(data) and len(zlib.decompress(compressed)) > 0, path
    return size

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--manifest', action='store_true')
    args = parser.parse_args()
    pack = ROOT / 'Intersect.Client/resources'
    files = sorted(pack.glob('*/guild_hq_*_v1.*'))
    assert len(files) == 15, f'Expected 6 PNG, 8 WAV, 1 OGG; got {len(files)}'
    result = []
    for path in files:
        entry = {'path': path.relative_to(pack).as_posix(), 'bytes': path.stat().st_size,
                 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
        if path.suffix == '.png': entry.update(png(path))
        else:
            info = sf.info(path)
            data, rate = sf.read(path, always_2d=True)
            assert rate == 44100 and np.isfinite(data).all(), path
            peak = float(np.max(np.abs(data)))
            rms = float(np.sqrt(np.mean(data * data)))
            assert peak < .98 and rms > .0001 and info.frames > 0, path
            assert info.channels == (2 if path.suffix == '.ogg' else 1), path
            assert path.suffix != '.wav' or info.subtype == 'PCM_16', path
            entry.update(seconds=info.duration, channels=info.channels, sample_rate=rate, peak=peak, rms=rms)
            if 'hearth' in path.name or 'theme' in path.name:
                entry['loop_join_delta'] = float(np.max(np.abs(data[0] - data[-1])))
                assert entry['loop_join_delta'] < .03, (path, entry)
        result.append(entry)
    if args.manifest:
        (ROOT / 'Documentation/GuildHeadquarters-Assets.json').write_text(
            json.dumps({'files': result}, indent=2) + '\n', encoding='utf-8')
    print(f'PASS: {len(result)} assets, PNG integrity, audio decoding, no clipping, loop joins.')

if __name__ == '__main__': main()
