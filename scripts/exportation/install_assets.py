#!/usr/bin/env python3
"""Install the approved Exportation ZIP safely into the Corps Royaux source tree.

Usage:
  python scripts/exportation/install_assets.py "/path/to/Corps_Royaux_Exportation_GFX_Sons.zip"
  python scripts/exportation/install_assets.py "/path/to/zip" --check
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import struct
import sys
import wave
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "resources" / "gui" / "exportation" / "assets.json"


def verify(data: bytes, record: dict) -> None:
    actual = hashlib.sha256(data).hexdigest()
    if actual != record["sha256"]:
        raise ValueError(f"SHA-256 mismatch: {record['path']}")
    if record["path"].endswith(".png"):
        if not data.startswith(b"\x89PNG\r\n\x1a\n"):
            raise ValueError(f"Not a PNG: {record['path']}")
        width, height = struct.unpack(">II", data[16:24])
        if (width, height) != (record["width"], record["height"]):
            raise ValueError(f"Unexpected PNG dimensions: {record['path']}")
    else:
        with wave.open(io.BytesIO(data), "rb") as sound:
            if (sound.getframerate(), sound.getnchannels(), sound.getsampwidth()) != (
                record["sampleRate"], 1, 2
            ):
                raise ValueError(f"Invalid WAV format: {record['path']}")


def install(archive: Path, check: bool) -> None:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    with ZipFile(archive) as zip_file:
        validated = []
        # Validate the entire archive *before* writing any asset.
        for item in manifest["assets"]:
            # Some legacy ZIP tools encode the accented background filename
            # as CP437 without the UTF-8 flag. Match only that known entry,
            # then verify the original bytes against the SHA-256 manifest.
            try:
                data = zip_file.read(item["zip"])
            except KeyError:
                if item["path"] != "resources/gui/exportation/background.png":
                    raise
                candidates = [name for name in zip_file.namelist()
                              if name.startswith("exportation_work/panneau_nautique_")
                              and name.endswith("_vide_et_localisable.png")]
                if len(candidates) != 1:
                    raise ValueError("Approved background not found in ZIP")
                data = zip_file.read(candidates[0])
            verify(data, item)
            destination = (ROOT / item["path"]).resolve()
            if not destination.is_relative_to(ROOT):
                raise ValueError("Unsafe destination path")
            validated.append((destination, data))
        if check:
            print(f"OK: {len(validated)} approved Exportation assets verified; no files changed.")
            return
        for destination, data in validated:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
            print(f"+ {destination.relative_to(ROOT)}")
    print(f"Installed {len(validated)} assets. Rebuild the client and editor to copy them to output.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path, help="Approved Exportation ZIP")
    parser.add_argument("--check", action="store_true", help="Validate without installing")
    args = parser.parse_args()
    try:
        install(args.archive, args.check)
    except (ValueError, OSError, KeyError, json.JSONDecodeError) as exc:
        sys.exit(f"Exportation asset installation refused: {exc}")
