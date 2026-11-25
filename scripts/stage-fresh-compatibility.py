#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-only
"""Rebuild the core compatibility layer from upstream DLLs without touching the game."""

import argparse
import hashlib
from pathlib import Path
import subprocess
import tempfile


REPO = Path(__file__).resolve().parents[1]
TOOLS = REPO / "implementation/tools"
RESOURCES = {
    "Vivify": (("Vivify.Resources.DepthBlit", 1746663828),),
    "AudioLink": (("AudioLink.Assets.Bundle", 2240245297),),
    "BeatLeader": (("BeatLeader._9_Resources.AssetBundles.asset_bundle", 204735007),),
    "CustomWalls": tuple(
        (f"CustomWalls.Resources.Materials.{name}.pixie", crc)
        for name, crc in (
            ("MysticalSnowWalls", 3588584398),
            ("PixelWalls", 1293538163),
            ("PlainWalls", 2875639925),
            ("TransparentWalls", 867766980),
        )
    ),
}


def digest(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("resources", type=Path)
    parser.add_argument("stage", type=Path, help="new output directory")
    parser.add_argument(
        "--backend", type=Path,
        default=Path.home() / "Library/Application Support/bsmac/mod-converter",
    )
    args = parser.parse_args()
    if args.stage.exists():
        parser.error("the stage directory already exists")
    runtime = args.backend.resolve() / "automatic"
    worker = runtime / "venv/bin/python"
    managed = args.resources / "Beat Saber_Data/Managed"
    plugins = args.resources / "Plugins"
    inputs = [managed / "BGLib.UnityExtension.dll", worker, runtime / "compile-metal"]
    inputs += [
        plugins / f"{name}.dll" for name in (
            *RESOURCES, "Chroma", "NoodleExtensions", "CustomNotes", "CustomSabersLite"
        )
    ]
    missing = [str(path) for path in inputs if not path.is_file()]
    if missing:
        parser.error("missing prerequisites:\n" + "\n".join(missing))

    with tempfile.TemporaryDirectory(prefix="bsmac-compatibility-") as folder:
        scratch = Path(folder)
        bridges = scratch / "bridges"
        prepared = scratch / "Plugins"
        prepared.mkdir()

        def build(project, output):
            subprocess.run([
                "dotnet", "build", "--disable-build-servers", str(TOOLS / project), "-c", "Release", "-o", str(output),
                f"-p:GameResources={args.resources.resolve()}", f"-p:UnityManaged={managed.resolve()}",
            ], check=True)

        build("AssemblyTool", scratch / "tool")
        for project in ("LocalMediaBridge", "NativeDiagnostics", "NativeAvatarRenderer",
                        "QosmeticsBridge", "VivifyMetalBridge"):
            build(project, bridges)

        def patch(*argv):
            subprocess.run(["dotnet", scratch / "tool/AssemblyTool.dll", *argv], check=True)

        def convert(*argv):
            command = [worker, "-I", "-m", "bsmac_converter.cli", "--backend", args.backend,
                       "--cache", args.backend / "cache/pipeline", *argv]
            return subprocess.check_output(command, text=True).strip()

        for mod, bundles in RESOURCES.items():
            assembly = plugins / f"{mod}.dll"
            for index, (name, expected_crc) in enumerate(bundles):
                print(f"Preparing {name}", flush=True)
                source = scratch / "original.bundle"
                patch("resource", assembly, name, source)
                original_crc = int(convert("crc", source))
                if original_crc != expected_crc:
                    raise ValueError(f"{name}: expected upstream CRC {expected_crc}, got {original_crc}")
                converted = Path(convert("convert", source))
                derivative_crc = int(convert("crc", converted))
                replaced = prepared / f"{mod}-{index}.dll"
                patch("replace-resource", assembly, name, digest(source),
                      converted, digest(converted), replaced)
                assembly = replaced
                if mod in {"Vivify", "AudioLink"} and original_crc != derivative_crc:
                    assembly = prepared / f"{mod}-{index}-crc.dll"
                    patch("crc", replaced, str(original_crc), str(derivative_crc), assembly)
            assembly.rename(prepared / f"{mod}.dll")

        subprocess.run([
            "bash", REPO / "scripts/stage-managed-compatibility.sh", scratch / "tool/AssemblyTool.dll",
            args.resources, bridges, args.stage, prepared,
        ], check=True)


if __name__ == "__main__":
    main()
