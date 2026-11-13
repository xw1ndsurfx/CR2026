"""Package published CR2026 clients and matching server binaries. Python 3.10+."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import tarfile
import urllib.request
import uuid
import zipfile

ROOT = Path(__file__).resolve().parent.parent
STEAM_VERSION = "2024.8.0"
STEAM_URL = f"https://github.com/rlabrecque/Steamworks.NET/releases/download/{STEAM_VERSION}/Steamworks.NET-Standalone_{STEAM_VERSION}.zip"


def digest(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as f:
        for data in iter(lambda: f.read(1024 * 1024), b""):
            h.update(data)
    return h.hexdigest().upper()


def copy_file(source, target):
    source, target = Path(source), Path(target)
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists() or source.stat().st_size != target.stat().st_size or digest(source) != digest(target):
        shutil.copy2(source, target)
    return str(target)


def files(folder):
    return sorted(p for p in Path(folder).rglob("*") if p.is_file())


def release_metadata(apk):
    sha = digest(apk)
    for p in (ROOT / "artifacts/android/distribution").glob("*/release.json"):
        data = json.loads(p.read_text(encoding="utf-8-sig"))
        if data["Sha256"] == sha:
            return data
    raise RuntimeError("APK has no matching distribution release.json. Run dotnet publish -r android-arm64 first.")


def public_file(path):
    name = path.name.lower()
    return not (any(x in name for x in (".db", "translation-service.private", "keystore-password")) or
                path.suffix.lower() in (".bkey", ".keystore", ".jks", ".pem", ".pfx"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--android-apk", type=Path, default=ROOT / "artifacts/android/Release/android-arm64/com.logiklik.corpsroyaux-Signed.apk")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--resume", action="store_true", help="Resume an unfinished package; never removes existing files")
    args = parser.parse_args()
    apk = args.android_apk.resolve()
    meta = release_metadata(apk)
    inputs = ROOT / ".local/distribution-inputs"
    inputs.mkdir(parents=True, exist_ok=True)
    stage = inputs / ("resources-" + uuid.uuid4().hex)
    stage.mkdir()
    with zipfile.ZipFile(apk) as archive:
        for item in archive.infolist():
            if not item.filename.startswith("assets/resources/") or item.is_dir():
                continue
            relative = PurePosixPath(item.filename.removeprefix("assets/resources/"))
            if relative.is_absolute() or ".." in relative.parts:
                raise RuntimeError("Unsafe resource path in APK")
            target = stage.joinpath(*relative.parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(item) as src, target.open("wb") as dst:
                shutil.copyfileobj(src, dst)
    overlay = ROOT / "Intersect.Client/bin/Release/net8.0/win-x64/publish/resources"
    linux_overlay = ROOT / "Intersect.Client/bin/Release/net8.0/linux-x64/publish/resources"
    if not overlay.is_dir() or not linux_overlay.is_dir():
        raise RuntimeError("Publish the Windows and Linux clients first: dotnet publish -r all-x64")
    if {p.relative_to(overlay).as_posix() for p in files(overlay)} != {p.relative_to(linux_overlay).as_posix() for p in files(linux_overlay)}:
        raise RuntimeError("Windows and Linux resource sets differ; rerun all-x64")
    changed = 0
    for source in files(overlay):
        relative = source.relative_to(overlay)
        linux = linux_overlay / relative
        if not linux.is_file() or digest(linux) != digest(source):
            raise RuntimeError(f"Desktop resource versions differ: {relative}; rerun all-x64")
        target = stage / relative
        if not target.exists() or digest(target) != digest(source):
            changed += 1
        copy_file(source, target)
    key = ROOT / "Intersect.Network/bin/Release/keys/network.handshake.bkey.pub"
    if changed or meta["NetworkPublicKeySha256"] != digest(key):
        print(f"Synchronizing Android: {changed} resource changes; matching compiled server key", flush=True)
        subprocess.run(["pwsh", "-NoProfile", "-File", str(ROOT / "scripts/build-android-client.ps1"),
                        "-Distribution", "-Resources", str(stage), "-NetworkPublicKey", str(key),
                        "-Version", meta["Version"]], cwd=ROOT, check=True)
        apk = ROOT / "artifacts/android/Release/android-arm64/com.logiklik.corpsroyaux-Signed.apk"
        meta = release_metadata(apk)
    resource_files = files(stage)
    if len(resource_files) < 8000 or any(not public_file(p) for p in resource_files):
        raise RuntimeError("Incomplete or private client resource input")
    output = (args.output or ROOT / f"artifacts/distribution/CR2026-{meta['Version']}-pack{meta['VersionCode']}").resolve()
    if output.exists() and not args.resume:
        raise RuntimeError(f"Output already exists: {output}; choose --output or use --resume for an unfinished package")
    output.mkdir(parents=True, exist_ok=True)
    steam_path = inputs / f"Steamworks.NET-Standalone_{STEAM_VERSION}.zip"
    if not steam_path.exists():
        urllib.request.urlretrieve(STEAM_URL, steam_path)
    with zipfile.ZipFile(steam_path) as steam:
        for rid, label in (("win-x64", "Windows-x64"), ("linux-x64", "Linux-x64")):
            client = output / "Joueurs" / label
            client.mkdir(parents=True, exist_ok=True)
            published = ROOT / f"Intersect.Client/bin/Release/net8.0/{rid}/publish"
            for source in published.iterdir():
                if source.name == "resources" or source.suffix.lower() in (".xml", ".pdb"):
                    continue
                if source.is_dir():
                    shutil.copytree(source, client / source.name, dirs_exist_ok=True, copy_function=copy_file)
                else:
                    copy_file(source, client / source.name)
            shutil.copytree(stage, client / "resources", dirs_exist_ok=True, copy_function=copy_file)
            native = "Windows-x64/steam_api64.dll" if rid == "win-x64" else "OSX-Linux-x64/libsteam_api.so"
            (client / Path(native).name).write_bytes(steam.read(native))
            (client / "licenses/Steamworks.NET-Standalone-LICENSE.txt").write_bytes(steam.read("LICENSE.txt"))
            copy_file(ROOT / "Intersect.Client/LICENSE.md", client / "LICENSE.md")
            if rid == "win-x64":
                (client / "Jouer.cmd").write_text('@echo off\ncd /d "%~dp0"\nstart "" "%~dp0Corps Royaux.exe"\n', encoding="utf-8")
            else:
                (client / "jouer.sh").write_text('#!/bin/sh\nset -eu\nAPP_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\ncd "$APP_DIR"\nexport LD_LIBRARY_PATH="$APP_DIR${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"\nexec "$APP_DIR/Corps Royaux" "$@"\n', encoding="utf-8", newline="\n")
            (client / "LIRE-MOI.txt").write_text(PLAYER_README, encoding="utf-8")
            server = output / "Serveur" / label
            server.mkdir(parents=True, exist_ok=True)
            for source in (ROOT / f"Intersect.Server/bin/Release/net8.0/{rid}/publish").iterdir():
                if source.suffix.lower() in (".xml", ".pdb") or ".db" in source.name.lower():
                    continue
                if source.is_dir() and source.name not in ("wwwroot", "licenses", "runtimes"):
                    continue
                target = server / ("Configuration-exemples" if source.name.startswith("appsettings") else "") / source.name
                if source.is_dir():
                    shutil.copytree(source, target, dirs_exist_ok=True, copy_function=copy_file)
                else:
                    copy_file(source, target)
            print(f"Prepared {label}: complete client and matching server binaries", flush=True)
    android = output / "Joueurs/Android-arm64"
    android.mkdir(parents=True, exist_ok=True)
    copy_file(apk, android / meta["Apk"])
    released = next(p.parent for p in (ROOT / "artifacts/android/distribution").glob("*/release.json")
                    if json.loads(p.read_text(encoding="utf-8-sig"))["Sha256"] == meta["Sha256"])
    for name in ("INSTALLATION.txt", "SHA256SUMS.txt", "LICENSE.md", "release.json"):
        copy_file(released / name, android / name)
    (output / "Serveur/LIRE-MOI.txt").write_text(SERVER_README, encoding="utf-8")
    (output / "LIRE-MOI.txt").write_text(ROOT_README, encoding="utf-8")
    archives = output / "Archives-joueurs"
    archives.mkdir(exist_ok=True)
    with zipfile.ZipFile(archives / "CR2026-Windows-x64.zip", "w", compression=zipfile.ZIP_STORED) as z:
        base = output / "Joueurs/Windows-x64"
        for source in files(base):
            z.write(source, "CR2026-Windows-x64/" + source.relative_to(base).as_posix())
    base = output / "Joueurs/Linux-x64"
    with tarfile.open(archives / "CR2026-Linux-x64.tar.gz", "w:gz", compresslevel=2) as tar:
        for source in files(base):
            relative = "CR2026-Linux-x64/" + source.relative_to(base).as_posix()
            info = tar.gettarinfo(str(source), arcname=relative)
            with source.open("rb") as f:
                magic = f.read(4)
            info.mode = 0o755 if magic == b"\x7fELF" or source.name.endswith(".sh") else 0o644
            info.uid = info.gid = 0
            info.uname = info.gname = ""
            with source.open("rb") as f:
                tar.addfile(info, f)
    # Android already is an installable archive: do not duplicate its 700 MB here.
    (archives / "Android.txt").write_text("FR: APK dans ../Joueurs/Android-arm64\nEN: APK in ../Joueurs/Android-arm64\nES: APK en ../Joueurs/Android-arm64\n", encoding="utf-8")
    hashes = {p.relative_to(output).as_posix(): digest(p) for p in files(output / "Joueurs")}
    if any(not public_file(p) for p in files(output / "Joueurs")):
        raise RuntimeError("Private file detected in player folders")
    archive_hashes = {p.relative_to(output).as_posix(): digest(p) for p in files(archives)}
    manifest = dict(version=meta["Version"], androidVersionCode=meta["VersionCode"],
                    resourceFiles=len(resource_files), networkPublicKeySha256=digest(key),
                    clients=hashes, archives=archive_hashes, serverDataIncluded=False,
                    steamNativeVersion=STEAM_VERSION, steamNativeSource=STEAM_URL)
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    (output / "SHA256SUMS.txt").write_text("".join(f"{sha}  {name}\n" for name, sha in {**archive_hashes, **{p: h for p, h in hashes.items() if p.endswith('.apk')}}.items()), encoding="ascii")
    print(f"Distribution folder: {output}", flush=True)


PLAYER_README = """CORPS ROYAUX 0.8.0
FR : Windows : lancer Jouer.cmd. Linux : extraire le .tar.gz puis lancer ./jouer.sh
(ou chmod +x jouer.sh 'Corps Royaux' si les permissions ont été perdues).
Conserver tout le dossier resources et les bibliothèques livrées. Connexion Internet
et compte de jeu habituel requis. Les ressources sont incluses.
Android : toucher un objet/sort pour ouvrir sa fiche, puis utiliser son bouton
d’action; un toucher active les raccourcis. Voir INSTALLATION.txt près de l’APK.

EN: Windows: run Jouer.cmd. Linux: extract the .tar.gz and run ./jouer.sh
(or chmod +x jouer.sh 'Corps Royaux' if permissions were lost).
Keep all resources and supplied libraries. Internet and a regular game account
are required. Resources are included. On Android, tap an item/spell to open its
description and action button; tap hotbar shortcuts once. See INSTALLATION.txt.

ES: Windows: ejecutar Jouer.cmd. Linux: extraer el .tar.gz y ejecutar ./jouer.sh
(o chmod +x jouer.sh 'Corps Royaux' si se perdieron los permisos).
Conservar resources y las bibliotecas incluidas. Se necesita Internet y una cuenta
del juego. Android: tocar un objeto/hechizo para abrir su ficha y botón de acción;
un toque activa los accesos rápidos. Ver INSTALLATION.txt junto al APK.
"""

SERVER_README = """ADMINISTRATION — SERVEURS CORRESPONDANTS
Exécutables Windows-x64 et Linux-x64 à appliquer à votre serveur existant.
Arrêter le serveur avant de remplacer les binaires. Conserver resources, bases de
données et configurations existantes. Les appsettings fournis sont uniquement
des modèles dans Configuration-exemples. Aucune donnée du monde ni base de comptes
n’est incluse. Déployer les clients de ce paquet avec ces serveurs compilés : leur
clé réseau diffère de celle de l’ancien serveur encore en ligne. Les APK de test
destinés au serveur actuel sont conservés séparément; ne pas mélanger ces ensembles.
Ne pas distribuer ce dossier Serveur aux joueurs. Aucun déploiement n’a été fait.
"""

ROOT_README = """CR2026 — DOSSIER DE DISTRIBUTION
FR : Joueurs contient les clients complets Windows, Linux et l’APK Android.
Archives-joueurs contient les téléchargements de bureau et le chemin de l’APK.
Serveur contient les binaires correspondants réservés à l’administration.
Ne partager avec les joueurs que Joueurs ou Archives-joueurs, pas le dossier complet.
Les clients visent le nouveau serveur compilé, à déployer sur l’installation existante.
EN: Share only Joueurs or Archives-joueurs. Serveur is administrator-only and
contains matching server binaries; existing world databases/configuration are excluded.
ES: Compartir solo Joueurs o Archives-joueurs. Serveur es exclusivo del administrador;
contiene los servidores correspondientes, sin datos del mundo ni cuentas de jugadores.
"""

if __name__ == "__main__":
    main()
