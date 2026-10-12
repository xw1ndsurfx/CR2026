#!/usr/bin/env python3
"""Package the original, unchanged Admurin PNG sprites for the client.

Usage:
    python -m pip install brotli
    python scripts/import-admurin-gui.py /path/to/new_gui.zip
This writes resources/gui/admurin-ui.b64 which is copied into client builds.
The client extracts all PNGs to resources/gui/admurin/ before loading GUI art.
"""
import base64
import io
import sys
import zipfile
from pathlib import Path

try:
    import brotli
except ImportError as exc:
    raise SystemExit("Install brotli: python -m pip install brotli") from exc

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


def main(path: Path) -> None:
    with zipfile.ZipFile(path) as source:
        files = {
            Path(name).name: source.read(name)
            for name in source.namelist()
            if name.lower().endswith(".png")
        }
    missing = EXPECTED - files.keys()
    if missing:
        raise SystemExit(f"Missing Admurin sprites: {', '.join(sorted(missing))}")

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
    print(f"Packaged {len(EXPECTED)} sprites into {target}")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python scripts/import-admurin-gui.py new_gui.zip")
    main(Path(sys.argv[1]))
