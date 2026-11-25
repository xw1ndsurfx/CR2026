"""Validate the checked-in approved Exportation media and localization catalog."""
import json
import re
import struct
import wave
from pathlib import Path
from install_assets import verify

ROOT = Path(__file__).resolve().parents[2]
manifest = json.loads((ROOT / 'resources/gui/exportation/assets.json').read_text(encoding='utf-8'))
for asset in manifest['assets']:
    file = ROOT / asset['path']
    verify(file.read_bytes(), asset)
    if file.suffix == '.wav':
        with wave.open(str(file), 'rb') as sound:
            assert 0 < sound.getnframes() / sound.getframerate() <= 5, f'Invalid cue duration: {file}'
            samples = struct.unpack('<' + 'h' * sound.getnframes(), sound.readframes(sound.getnframes()))
            assert 0 < max(abs(s) for s in samples) < 32767, f'Silent or clipped cue: {file}'
catalog = json.loads((ROOT / 'Framework/Intersect.Framework.Core/Exportation/Text.json').read_text(encoding='utf-8-sig'))
entries = catalog['entries'] if 'entries' in catalog else catalog['Entries']
for source, translations in entries.items():
    for language in ('en', 'fr', 'es'):
        assert translations.get(language), f'Missing {language}: {source}'
        assert sorted(re.findall(r'\{[^{}]+\}', source)) == sorted(re.findall(r'\{[^{}]+\}', translations[language])), f'Placeholder mismatch: {source}/{language}'
print(f"PASS: {len(manifest['assets'])} approved images/sounds; EN/FR/ES catalog and placeholders")
