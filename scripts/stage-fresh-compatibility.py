#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-only
"""Rebuild the core compatibility layer from upstream DLLs without touching the game."""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile


REPO = Path(__file__).resolve().parents[1]
TOOLS = REPO / "implementation/tools"
CAMERA2_ORIGINAL_SHA256 = "a4f0b037618dea16e70bbf1815cb5701d5f9eeaccb1e04c47f20bc6368e9edb9"
RESOURCES = {
    "Camera2": (("Camera2.Shaders.camera2utils", 1881945326),),
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
    parser.add_argument("--camera2-only", action="store_true", help="repair only upstream Camera2 0.6.118")
    args = parser.parse_args()
    bundles_to_stage = {"Camera2": RESOURCES["Camera2"]} if args.camera2_only else RESOURCES
    if args.stage.exists():
        parser.error("the stage directory already exists")
    runtime = args.backend.resolve() / "automatic"
    worker = runtime / "venv/bin/python"
    managed = args.resources / "Beat Saber_Data/Managed"
    plugins = args.resources / "Plugins"
    inputs = [worker, runtime / "compile-metal"]
    inputs.extend(plugins / f"{name}.dll" for name in bundles_to_stage)
    if not args.camera2_only:
        inputs.append(managed / "BGLib.UnityExtension.dll")
        inputs.extend(plugins / f"{name}.dll" for name in (
            "Chroma", "NoodleExtensions", "CustomNotes", "CustomSabersLite"
        ))
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
        if not args.camera2_only:
            for project in ("LocalMediaBridge", "NativeDiagnostics", "NativeAvatarRenderer",
                            "QosmeticsBridge", "VivifyMetalBridge"):
                build(project, bridges)

        def patch(*argv):
            subprocess.run(["dotnet", scratch / "tool/AssemblyTool.dll", *argv], check=True)

        def convert(*argv):
            command = [worker, "-I", "-m", "bsmac_converter.cli", "--backend", args.backend,
                       "--cache", args.backend / "cache/pipeline", *argv]
            return subprocess.check_output(command, text=True).strip()

        for mod, bundles in bundles_to_stage.items():
            assembly = plugins / f"{mod}.dll"
            original_assembly_sha = digest(assembly)
            for index, (name, expected_crc) in enumerate(bundles):
                print(f"Preparing {name}", flush=True)
                source = scratch / "original.bundle"
                patch("resource", assembly, name, source)
                source_sha = digest(source)
                if mod == "Camera2" and source_sha != CAMERA2_ORIGINAL_SHA256:
                    raise ValueError("Unexpected Camera2 0.6.118 shader bundle. Check whether it was already patched.")
                original_crc = int(convert("crc", source))
                if original_crc != expected_crc:
                    raise ValueError(f"{name}: expected upstream CRC {expected_crc}, got {original_crc}")
                converted = Path(convert("convert", source))
                derivative_crc = int(convert("crc", converted))
                replaced = prepared / f"{mod}-{index}.dll"
                patch("replace-resource", assembly, name, source_sha,
                      converted, digest(converted), replaced)
                assembly = replaced
                if mod in {"Vivify", "AudioLink"} and original_crc != derivative_crc:
                    assembly = prepared / f"{mod}-{index}-crc.dll"
                    patch("crc", replaced, str(original_crc), str(derivative_crc), assembly)
            assembly.rename(prepared / f"{mod}.dll")

        if args.camera2_only:
            relative = "Plugins/Camera2.dll"
            staged = args.stage / relative
            staged.parent.mkdir(parents=True, exist_ok=False)
            staged.write_bytes((prepared / staged.name).read_bytes())
            plan = args.stage / "plan.json"
            plan.write_text(json.dumps([dict(source=relative, target=relative,
                                            sha256=digest(staged),
                                            previousSha256=original_assembly_sha)], indent=2) + "\n")
            print(plan)
            return

        subprocess.run([
            "bash", REPO / "scripts/stage-managed-compatibility.sh", scratch / "tool/AssemblyTool.dll",
            args.resources, bridges, args.stage, prepared,
        ], check=True)


if __name__ == "__main__":
    main()
