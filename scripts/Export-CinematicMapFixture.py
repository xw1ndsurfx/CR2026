"""Export a read-only copy of native map data for the optional GPU smoke check.

Never bundles game content in the repository and never opens the player database.
Use a snapshot of gamedata.db, not a file being modified by a live server.
"""
import argparse
import json
import sqlite3
from pathlib import Path


def export(database: Path, options: Path, map_id: str, output: Path):
    output.mkdir(parents=True, exist_ok=False)
    with sqlite3.connect(database.resolve().as_uri() + '?mode=ro', uri=True) as db:
        db.row_factory = sqlite3.Row
        if db.execute('PRAGMA quick_check').fetchone()[0] != 'ok':
            raise ValueError('The game database snapshot failed its integrity check.')
        record = db.execute('SELECT * FROM Maps WHERE lower(Id)=lower(?)', (map_id,)).fetchone()
        if record is None:
            raise ValueError('Map ID not found in the game database snapshot.')
        row = dict(record)
        (output / 'tiles.bin').write_bytes(row.pop('TileData'))
        (output / 'attributes.bin').write_bytes(row.pop('Attributes'))
        row['EventIds'] = json.loads(row.pop('Events') or '[]')
        for db_name, json_name in [('Lights', 'Lights'), ('NpcSpawns', 'Spawns'),
                                   ('PlayerLightColor', 'PlayerLightColor'), ('WeatherEffects', 'WeatherEffects')]:
            if row.get(db_name):
                row[json_name] = json.loads(row.pop(db_name))
        (output / 'map.json').write_text(json.dumps(row, ensure_ascii=False), encoding='utf-8')
        tilesets = [dict(t) for t in db.execute('SELECT Id,Name FROM Tilesets')]
        (output / 'tilesets.json').write_text(json.dumps(tilesets), encoding='utf-8')
        events = []
        for record in db.execute('SELECT * FROM Events WHERE lower(MapId)=lower(?)', (map_id,)):
            event = dict(record)
            event['Pages'] = json.loads(event['Pages'] or '[]')
            events.append(event)
        (output / 'events.json').write_text(json.dumps(events, ensure_ascii=False), encoding='utf-8')
    configuration = json.loads(options.read_text(encoding='utf-8-sig'))
    # Only rendering options: no credentials, database connections or SMTP settings.
    (output / 'options.json').write_text(json.dumps({key: configuration[key] for key in ('Map', 'AnimatedSprites') if key in configuration}), encoding='utf-8')
    print(f"Exported {row['Name']} ({row['Id']}), {len(events)} native Events. No account data exported.")


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('database', type=Path)
    parser.add_argument('options', type=Path)
    parser.add_argument('map_id')
    parser.add_argument('output', type=Path, help='New folder outside the source checkout')
    arguments = parser.parse_args()
    export(arguments.database, arguments.options, arguments.map_id, arguments.output)
