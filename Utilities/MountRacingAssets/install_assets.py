"""Install only the eleven manifest-listed racing assets. Never replace executables or configuration."""
from pathlib import Path
import argparse, hashlib, json, shutil, datetime

ROOT = Path(__file__).resolve().parents[2]
EXPECTED = {'resources/gui/racing_background_v1.png', 'resources/gui/racing_icons_v1.png'} | {
    f'resources/sounds/racing/{name}_v1.wav' for name in ('open','ready','countdown','start','checkpoint','lap','finish','reward','cancel')}

def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def install(destination, apply=False):
    destination = Path(destination).resolve(strict=True)
    if not (destination/'resources').is_dir(): raise ValueError('Choose the existing client/editor folder containing resources.')
    manifest = json.loads((ROOT/'Documentation/Mount-Racing-Assets.json').read_text(encoding='utf-8'))
    assets = manifest['audio'] + manifest['images']
    relative = {str(Path(e['file']).relative_to('Intersect.Client')).replace('\\','/') for e in assets}
    if relative != EXPECTED or len(assets) != 11: raise ValueError('Unexpected asset manifest.')
    planned = []
    for entry in assets:
        source = (ROOT/entry['file']).resolve(strict=True)
        source.relative_to((ROOT/'Intersect.Client/resources').resolve())
        if digest(source) != entry['sha256']: raise ValueError('Source hash mismatch: '+str(source))
        target = (destination/Path(entry['file']).relative_to('Intersect.Client')).resolve()
        target.relative_to(destination)
        action = 'identical' if target.is_file() and digest(target) == entry['sha256'] else 'replace' if target.exists() else 'add'
        planned.append((source, target, entry['sha256'], action))
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    backup = ROOT/'validation-local'/('mount-racing-assets-'+stamp)
    report = {'destination':str(destination), 'applied':apply, 'files':[]}
    for source,target,sha,action in planned:
        rel = target.relative_to(destination)
        if apply and action != 'identical':
            if action == 'replace':
                copy = backup/'backup'/rel; copy.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(target,copy)
            target.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(source,target)
        verified = target.is_file() and digest(target) == sha
        report['files'].append({'file':rel.as_posix(),'action':action,'verified':verified,'sha256':sha})
        if apply and not verified: raise IOError('Installed hash mismatch: '+str(target))
    if apply:
        backup.mkdir(parents=True,exist_ok=True)
        (backup/'installation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
        print('Report:',backup/'installation.json')
    print(json.dumps({a:sum(row[3]==a for row in planned) for a in ('add','replace','identical')}))
    print('Applied and verified 11 assets.' if apply else 'Dry run: no files changed.')

if __name__ == '__main__':
    p=argparse.ArgumentParser(); p.add_argument('destination'); p.add_argument('--apply',action='store_true'); args=p.parse_args()
    install(args.destination,args.apply)
