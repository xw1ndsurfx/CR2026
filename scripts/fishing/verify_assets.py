#!/usr/bin/env python3
"""Read-only checks for the delivered fishing art/audio pack. Requires Pillow.

Run from any directory: python scripts/fishing/verify_assets.py
No source image or sound is rewritten by this verifier.
"""
from __future__ import annotations

import argparse
import array
import hashlib
import json
from pathlib import Path
import sys
import wave

from PIL import Image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    errors: list[str] = []
    checks = 0

    def check(condition: bool, message: str) -> None:
        nonlocal checks
        checks += 1
        if not condition:
            errors.append(message)

    def read_json(relative: str):
        return json.loads((root / relative).read_text(encoding="utf-8"))

    manifest = read_json("resources/fishing/assets-manifest.json")
    images: dict[str, Image.Image] = {}
    for entry in manifest["assets"]:
        path = root / entry["repository_path"]
        check(path.is_file(), f"Missing image: {path}")
        if not path.is_file():
            continue
        check(hashlib.sha256(path.read_bytes()).hexdigest() == entry["sha256"], f"Changed image bytes: {path}")
        im = Image.open(path)
        im.load()
        images[entry["runtime_path"].removeprefix("resources/gui/")] = im
        check(im.size == (entry["width"], entry["height"]), f"Wrong dimensions: {path}")
        if entry.get("source") == "imagegen":
            check(im.mode == "RGBA", f"Generated atlas needs alpha: {path}")
            if im.mode == "RGBA":
                lo, hi = im.getchannel("A").getextrema()
                check(lo == 0 and hi > 0, f"Generated atlas needs transparent and visible pixels: {path}")

    def rect(file: str, source: list[int]) -> None:
        im = images.get(file)
        check(im is not None, f"Unknown atlas: {file}")
        if im is None:
            return
        x, y, w, h = source
        check(x >= 0 and y >= 0 and w > 0 and h > 0 and x + w <= im.width and y + h <= im.height,
              f"Out-of-bounds source rectangle: {file} {source}")

    atlas = read_json("resources/fishing/atlas-layout.json")
    for entry in atlas["components"].values():
        rect(entry["file"], entry["source"])
    for entry in atlas["effects"].values():
        check(len(entry["frames"]) == 4 and entry["fps"] > 0, "Invalid effect animation")
        for frame in entry["frames"]:
            rect(entry["file"], frame)
    shell = atlas["shell"]
    for name in ("scene", "xp_track", "line_track"):
        rect(shell["file"], shell[name])

    catalog = read_json("resources/fishing/asset-catalog.json")
    used = set()
    for preset in catalog["background_presets"]:
        for field in ("Background", "Foreground", "Decorations"):
            if preset[field]:
                check(preset[field] in images, f"Unknown preset image: {preset[field]}")
                used.add(preset[field])
        check(set(preset["name"]) == {"en", "fr", "es"}, "Missing preset translation")
    for number in range(1, 6):
        check(f"fishing/backgrounds/Water_Background{number}.png" in used, f"Background {number} omitted")
    for grid in catalog["sprite_grids"]:
        for cell in grid["occupied_cells"]:
            rect(grid["file"], cell["source_rect"])
    for clip in catalog["world_animation"]["clips"]:
        for layer in ("front", "back"):
            if not clip[layer]:
                continue
            im = images.get(clip[layer])
            check(im is not None and im.size == (128 * clip["frames"], 128 * 4),
                  f"Invalid world animation grid: {clip[layer]}")

    audio = read_json("resources/fishing/audio-manifest.json")
    sound_ids = {a["id"] for a in audio["assets"]}
    max_peak = 0
    for sound in audio["assets"]:
        path = root / "resources/sounds" / sound["runtime_path"]
        check(path.is_file(), f"Missing sound: {path}")
        if not path.is_file():
            continue
        check(hashlib.sha256(path.read_bytes()).hexdigest() == sound["sha256"], f"Changed sound bytes: {path}")
        with wave.open(str(path), "rb") as wav:
            check(wav.getsampwidth() == 2 and wav.getframerate() == 44100, f"Invalid PCM format: {path}")
            channels = wav.getnchannels()
            check(channels == sound["channels"], f"Wrong channel count: {path}")
            samples = array.array("h", wav.readframes(wav.getnframes()))
            if sys.byteorder != "little":
                samples.byteswap()
        peak = max(map(abs, samples), default=0)
        max_peak = max(max_peak, peak)
        check(0 < peak < 32767, f"Silent or clipped sound: {path}")
        if sound["loop"]:
            check(max(abs(samples[c] - samples[-channels + c]) for c in range(channels)) <= 2,
                  f"Loop boundary discontinuity: {path}")
        else:
            check(all(v == 0 for v in samples[:channels] + samples[-channels:]), f"Nonzero one-shot endpoint: {path}")
    for event, binding in audio["events"].items():
        check(binding["sound_id"] is None or binding["sound_id"] in sound_ids, f"Unknown sound at event {event}")

    report = {"checks": checks, "passed": not errors, "errors": errors,
              "images": len(manifest["assets"]), "audio_files": len(audio["assets"]),
              "audio_event_bindings": len(audio["events"]), "max_pcm_peak": max_peak,
              "scope": "File integrity, atlas bounds, source coverage and numerical audio checks. Not a listening or game-render test."}
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    raise SystemExit(0 if not errors else 1)


if __name__ == "__main__":
    main()
