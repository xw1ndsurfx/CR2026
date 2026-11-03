#!/usr/bin/env python3
"""Compile and execute the production fishing rules without MSBuild or external packages."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("--dotnet", default=shutil.which("dotnet"), help="Path to a .NET SDK's dotnet host")
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
output = Path(__file__).resolve().parent / "bin/standalone"
output.mkdir(parents=True, exist_ok=True)
usings = output / "GlobalUsings.cs"
usings.write_text("\n".join("global using " + name + ";" for name in
    ["System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Threading", "System.Threading.Tasks"]) + "\n")
sources = [path for path in (repo / "Framework/Intersect.Framework.Core/Fishing").glob("*.cs") if not path.name.endswith("Text.cs")]
sources += [repo / "Framework/Intersect.Framework.Core/MiniGames/MiniGameProgression.cs", Path(__file__).with_name("Program.cs"), usings]
arguments = ["-nologo", "-target:exe", "-langversion:latest", "-nullable:enable", f'-out:"{output / "FishingRulesTests.dll"}"']
arguments += [f'-reference:"{path}"' for path in (reference_pack / "ref/net8.0").glob("*.dll")]
arguments += [f'"{path}"' for path in sources]
response = output / "compile.rsp"
response.write_text("\n".join(arguments) + "\n")
(output / "FishingRulesTests.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
    "tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime.name}}}))
shutil.copyfile(repo / "Framework/Intersect.Framework.Core/Fishing/Text.json", output / "Text.json")
subprocess.run([str(host), "exec", str(sdk / "Roslyn/bincore/csc.dll"), "@" + str(response)], check=True)
subprocess.run([str(host), str(output / "FishingRulesTests.dll")], check=True)
