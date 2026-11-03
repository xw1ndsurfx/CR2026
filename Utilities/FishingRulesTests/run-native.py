#!/usr/bin/env python3
"""Compile only the headless tests against already-built server assemblies; never rebuild shared projects."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("--dotnet", default=shutil.which("dotnet"))
parser.add_argument("--configuration", default="Debug")
args = parser.parse_args()
if not args.dotnet:
    parser.error("Supply --dotnet or install the .NET 8 SDK.")
host = Path(args.dotnet).resolve()
sdk_root = host.parent
repo = Path(__file__).resolve().parents[2]
version = lambda path: tuple(int(part) for part in path.name.split(".") if part.isdecimal())
sdk = max((sdk_root / "sdk").glob("8.*"), key=version)
reference_pack = max((sdk_root / "packs/Microsoft.NETCore.App.Ref").glob("8.*"), key=version)
runtime = max((sdk_root / "shared/Microsoft.NETCore.App").glob("8.*"), key=version)
output = Path(__file__).resolve().parent / "bin/native"
output.mkdir(parents=True, exist_ok=True)
assets = json.loads((repo / "Intersect.Server.Core/obj/project.assets.json").read_text())
cache = Path(next(iter(assets["packageFolders"])))
references = {path.name: path for path in (reference_pack / "ref/net8.0").glob("*.dll")}
for library, target in assets["targets"]["net8.0"].items():
    definition = assets["libraries"][library]
    if definition["type"] != "package":
        continue
    base = cache / definition["path"]
    for relative in target.get("compile", {}):
        path = base / relative
        if path.suffix == ".dll" and path.exists():
            references[path.name] = path
    for relative in target.get("runtime", {}):
        path = base / relative
        if path.suffix == ".dll" and path.exists():
            shutil.copy2(path, output / path.name)
server = repo / f"Intersect.Server.Core/bin/{args.configuration}/net8.0"
if not (server / "Intersect.Server.Core.dll").exists():
    parser.error("Build Intersect.Server.Core first, or use the standalone shared-rule suite.")
for path in server.glob("*.dll"):
    references[path.name] = path
    shutil.copy2(path, output / path.name)
core = repo / f"Framework/Intersect.Framework.Core/bin/{args.configuration}/net8.0/Intersect.Framework.Core.dll"
references[core.name] = core
shutil.copy2(core, output / core.name)
# Transitive netstandard packages include old framework facades. Prefer net8 reference
# assemblies while retaining explicit package upgrades used by the existing server graph.
upgraded = {"System.Collections.Immutable.dll", "System.Text.Json.dll", "System.Diagnostics.DiagnosticSource.dll",
            "System.IO.Pipelines.dll", "System.Numerics.Vectors.dll"}
for name in list(references):
    framework_reference = reference_pack / "ref/net8.0" / name
    if name not in upgraded and framework_reference.exists():
        references[name] = framework_reference
usings = output / "GlobalUsings.cs"
usings.write_text("\n".join("global using " + name + ";" for name in
    ["System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Threading", "System.Threading.Tasks"]) + "\n")
sources = [Path(__file__).with_name("Program.cs"), Path(__file__).with_name("ServerRewardChecks.cs"), usings]
arguments = ["-nologo", "-target:exe", "-langversion:latest", "-nullable:enable", "-define:FISHING_SERVER_TESTS",
             f'-out:"{output / "Intersect.Tests.Server.dll"}"']
arguments += [f'-reference:"{path}"' for path in references.values()]
arguments += [f'"{path}"' for path in sources]
response = output / "compile.rsp"
response.write_text("\n".join(arguments) + "\n")
(output / "Intersect.Tests.Server.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
    "tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime.name}}}))
shutil.copyfile(repo / "Framework/Intersect.Framework.Core/Fishing/Text.json", output / "Text.json")
subprocess.run([str(host), "exec", str(sdk / "Roslyn/bincore/csc.dll"), "@" + str(response)], check=True)
subprocess.run([str(host), str(output / "Intersect.Tests.Server.dll")], check=True)
