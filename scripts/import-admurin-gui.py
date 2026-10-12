#!/usr/bin/env python3
"""Import Admurin's 37 PNG sprites from a ZIP *or* a resources\\gui folder.

Usage:
    python -m pip install brotli
    python scripts/import-admurin-gui.py "C:\\path\\to\\resources\\gui"
    python scripts/import-admurin-gui.py "C:\\path\\to\\new_gui.zip"

The generated resources/gui/admurin-ui.b64 belongs in source control; it
is automatically included in game and Steam builds and extracted on launch.
"""
import argparse
import base64
import io
import sys
import zipfile
from pathlib import Path

try:
    import brotli
except ImportError as exc:
    raise SystemExit("Install brotli first: python -m pip install brotli") from exc

EXPECTED = {
    "bordered_container.png", "chests.png",
    *(f"daily_{i}.png" for i in range(1, 5)),
    "dungeon_gates_0a.png", "dungeon_gates_0b.png",
    *(f"dungeon_gates_{i}.png" for i in range(1, 6)),
    *(f"logicoin_{i}.png" for i in range(12)),
    *(f"questlog_{i}.png" for i in range(3)),
    "shop_1.png", "shop_2.png",
    "stylist_0.png", "stylist_1.png",
    *(f"userlist_{i}.png" for i in range(5)),
}


def collect(source: Path) -> dict[str, bytes]:
    if not source.exists():
        raise SystemExit(f"Source not found: {source}")
    if source.is_dir():
        discovered = [
            (path.name, path.read_bytes())
            for path in sorted(source.rglob("*.png"))
        ]
    elif source.is_file() and zipfile.is_zipfile(source):
        with zipfile.ZipFile(source) as archive:
            discovered = [
                (Path(name).name, archive.read(name))
                for name in sorted(archive.namelist())
                if name.lower().endswith(".png")
            ]
    else:
        raise SystemExit(f"Expected a GUI image folder or ZIP archive: {source}")

    files: dict[str, bytes] = {}
    for name, content in discovered:
        key = name.lower()
        if key not in EXPECTED:
            continue
        if key in files and files[key] != content:
            raise SystemExit(f"Conflicting duplicates found for {key} in {source}")
        files[key] = content

    missing = EXPECTED - files.keys()
    if missing:
        raise SystemExit(
            f"Missing {len(missing)} Admurin sprites in {source}: "
            + ", ".join(sorted(missing))
        )
    return files


def main(source: Path) -> None:
    files = collect(source)
    payload = io.BytesIO()
    with zipfile.ZipFile(payload, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name in sorted(EXPECTED):
            archive.writestr(f"admurin/{name}", files[name])

    target = Path(__file__).resolve().parents[1] / "resources/gui/admurin-ui.b64"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(
        base64.b64encode(brotli.compress(payload.getvalue(), quality=11)).decode("ascii"),
        encoding="ascii",
    )
    print(f"Packaged {len(EXPECTED)} sprites from {source} into {target}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Path to new_gui.zip or the game resources/gui folder")
    args = parser.parse_args()
    main(args.source)
