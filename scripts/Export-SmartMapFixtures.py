"""Export isolated Smart Map Builder test data. Opens the source SQLite database read-only.

Never commit the export or copied game artwork. No player database or credentials are read.
"""
import argparse
import base64
import json
import shutil
import sqlite3
from pathlib import Path


def export(server: Path, client: Path, output: Path, tileset_output: Path) -> None:
    config = json.loads((server / "config.json").read_text(encoding="utf-8-sig"))
    with sqlite3.connect((server / "gamedata.db").resolve().as_uri() + "?mode=ro", uri=True) as database:
        database.row_factory = sqlite3.Row
        database.execute("BEGIN")
        maps = []
        for record in database.execute("SELECT * FROM Maps"):
            row = dict(record)
            tiles = row.pop("TileData")
            attributes = row.pop("Attributes")
            for old, new in [("Lights", "Lights"), ("Events", "EventIds"), ("NpcSpawns", "Spawns"), ("PlayerLightColor", "PlayerLightColor")]:
                value = row.pop(old, None)
                if value is not None:
                    try:
                        row[new] = json.loads(value)
                    except (ValueError, TypeError):
                        row[new] = value
            events = {}
            for event in database.execute("SELECT * FROM Events WHERE MapId=?", (row["Id"],)):
                event = dict(event)
                event["Pages"] = json.loads(event["Pages"] or "[]")
                events[event["Id"]] = event
            maps.append({"Metadata": row, "Tiles": base64.b64encode(tiles or b"").decode(),
                         "Attributes": base64.b64encode(attributes or b"").decode(), "Events": events})
        tilesets = [dict(row) for row in database.execute("SELECT Id, Name FROM Tilesets")]
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({"MapOptions": config["Map"], "Maps": maps, "Tilesets": tilesets}), encoding="utf-8")
    tileset_output.mkdir(parents=True, exist_ok=True)
    source_root = (client / "tilesets").resolve()
    destination_root = tileset_output.resolve()
    copied = 0
    for tile in tilesets:
        relative = Path(tile["Name"])
        if relative.is_absolute() or ".." in relative.parts:
            raise ValueError("Tileset name must stay inside the tileset resource directory")
        source = (source_root / relative).resolve()
        destination = (destination_root / relative).resolve()
        if not source.is_relative_to(source_root) or not destination.is_relative_to(destination_root):
            raise ValueError("Tileset path escapes its resource directory")
        if source == destination:
            raise ValueError("Choose a separate destination for test artwork")
        if source.is_file():
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            copied += 1
    print(f"Exported {len(maps)} maps and copied {copied} tilesets into the isolated test workspace.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--server-resources", type=Path, required=True)
    parser.add_argument("--client-resources", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--tileset-output", type=Path, required=True)
    args = parser.parse_args()
    export(args.server_resources, args.client_resources, args.output, args.tileset_output)
