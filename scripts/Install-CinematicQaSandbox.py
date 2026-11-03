"""Install the exported interactive scenario into an explicitly marked local QA copy only.

Stop the local server first. This never accesses or copies player/account data.
"""
import argparse
import json
import sqlite3
from pathlib import Path


def install(scenario_folder: Path, sandbox: Path):
    sandbox = sandbox.resolve()
    marker = sandbox / '.cinematic-test-sandbox'
    if not marker.is_file() or marker.read_text(encoding='utf-8').strip() != 'Royal Cinematic Director local QA only':
        raise ValueError('Refusing to modify an unmarked server directory.')
    resources = sandbox / 'resources'
    config = json.loads((resources / 'config.json').read_text(encoding='utf-8-sig'))
    if config.get('GameName') != 'CR2026 Cinematic Local Test':
        raise ValueError('Refusing to modify a server without the explicit local QA game name.')
    scenario = json.loads((scenario_folder / 'scenario.json').read_text(encoding='utf-8-sig'))
    database = resources / 'gamedata.db'
    if not database.is_file():
        raise ValueError('The local game database copy does not exist.')
    with sqlite3.connect(database) as db:
        if not db.execute('SELECT 1 FROM Maps WHERE lower(Id)=lower(?)', (scenario['MapId'],)).fetchone():
            raise ValueError('Scenario map is not present in this database.')
        backup = resources / 'gamedata.before-cinematic-qa.db'
        if not backup.exists():
            with sqlite3.connect(backup) as target:
                db.backup(target)

        def upsert(table, row):
            available = {column[1] for column in db.execute(f'PRAGMA table_info("{table}")')}
            keys = [key for key in row if key in available]
            columns = ','.join(f'"{key}"' for key in keys)
            placeholders = ','.join('?' for _ in keys)
            db.execute(f'INSERT OR REPLACE INTO "{table}" ({columns}) VALUES ({placeholders})', [row[key] for key in keys])

        with db:
            # Only this disposable copy: suspend original common Event auto triggers
            # so story/login flows cannot interfere with a repeatable QA scenario.
            for event_id, pages_json in db.execute('SELECT Id,Pages FROM Events WHERE CommonEvent=1').fetchall():
                pages = json.loads(pages_json or '[]')
                for page in pages:
                    page['CommonTrigger'] = 0
                db.execute('UPDATE Events SET Pages=? WHERE Id=?', (json.dumps(pages), event_id))
            for variable in scenario['Variables']:
                variable['Folder'] = 'QA Cinematics'
                upsert('PlayerVariables', variable)
            for event in scenario['Events']:
                event['Folder'] = 'QA Cinematics'
                upsert('Events', event)
            map_events = json.loads(db.execute('SELECT Events FROM Maps WHERE lower(Id)=lower(?)', (scenario['MapId'],)).fetchone()[0] or '[]')
            for event in scenario['Events']:
                if not event['CommonEvent'] and event['Id'].lower() not in {item.lower() for item in map_events}:
                    map_events.append(event['Id'])
            db.execute('UPDATE Maps SET Events=?, Revision=Revision+1 WHERE lower(Id)=lower(?)', (json.dumps(map_events), scenario['MapId']))
            db.execute('UPDATE Classes SET SpawnMap=?, SpawnX=?, SpawnY=?', (scenario['MapId'], scenario['StartX'], scenario['StartY']))
    languages = resources / 'languages'
    languages.mkdir(exist_ok=True)
    destination = languages / 'content.json'
    catalog = json.loads(destination.read_text(encoding='utf-8-sig')) if destination.exists() else {'Entries': {}}
    catalog.setdefault('Entries', {}).update(json.loads((scenario_folder / 'content.json').read_text(encoding='utf-8-sig'))['Entries'])
    destination.write_text(json.dumps(catalog, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f"Installed local QA scene at {scenario['StartX']},{scenario['StartY']}. Type /cinematicqa in the test client.")


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('scenario_folder', type=Path)
    parser.add_argument('sandbox', type=Path)
    arguments = parser.parse_args()
    install(arguments.scenario_folder, arguments.sandbox)
