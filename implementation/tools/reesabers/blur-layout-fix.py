# SPDX-License-Identifier: AGPL-3.0-only
import re
import struct
import subprocess
import UnityPy
from pathlib import Path

import argparse

parser = argparse.ArgumentParser(description="Correct and compile the verified ReeSabers blur layouts")
parser.add_argument("original", type=Path)
parser.add_argument("source", type=Path)
parser.add_argument("destination", type=Path)
args = parser.parse_args()
ORIGINAL, SOURCE, DEST = (p.resolve() for p in (args.original, args.source, args.destination))
if DEST in (ORIGINAL, SOURCE):
    parser.error("Use a separate output path")
OUT = DEST.parent / "ComputeDiagnostics" / "Packed"
OUT.mkdir(parents=True, exist_ok=True)
COMPILER = Path(__file__).resolve().with_name("reflect-compute.swift")

def find_blur(env):
    for obj in env.objects:
        if obj.type.name != "ComputeShader":
            continue

        d = obj.read_typetree()

        if d["m_Name"] == "ComputeBlurLibrary":
            return obj, d

    raise RuntimeError("ComputeBlurLibrary not found")

original_env = UnityPy.load(str(ORIGINAL))
env = UnityPy.load(str(SOURCE))

_, original = find_blur(original_env)
obj, converted = find_blur(env)

d3d = next(
    v for v in original["variants"]
    if v["targetRenderer"] == 2
)

metal = next(
    v for v in converted["variants"]
    if v["targetRenderer"] == 16
)

original_kernels = {
    k["name"]: k for k in d3d["kernels"]
}

failed = False
fixed = 0

for kernel in metal["kernels"]:
    name = kernel["name"]

    original_variants = original_kernels[name]["uniqueVariants"]
    metal_variants = kernel["uniqueVariants"]

    assert len(original_variants) == len(metal_variants)

    for i, (old, new) in enumerate(
        zip(original_variants, metal_variants)
    ):
        print("\n===", name, "===")

        cb = next(
            d3d["constantBuffers"][idx]
            for idx in old["cbVariantIndices"]
            if d3d["constantBuffers"][idx]["name"]
            in ("$Globals", "Globals")
        )

        expected = {
            p["name"]: p["offset"]
            for p in cb["params"]
        }

        expected_size = cb["byteSize"]

        raw = bytes(new["code"])
        magic, version, offset = struct.unpack_from("<III", raw)

        assert magic == 0xDEADCAFE

        msl = raw[offset:].rstrip(b"\0").decode()

        def pack_globals(match):
            body = match.group(2)

            body, count = re.subn(
                r"\b(float|int|uint)3(?=\s+\w+\s*;)",
                r"packed_\g<1>3",
                body
            )

            print("Packed vector declarations:", count)

            return match.group(1) + body + match.group(3)

        candidate, count = re.subn(
            r"(\bstruct\s+Globals_Type\s*\{)(.*?)(\}\s*;)",
            pack_globals,
            msl,
            flags=re.S
        )

        if count != 1:
            raise RuntimeError("Globals_Type not found uniquely")

        path = OUT / f"{name}-{i}.metal"
        path.write_text(candidate)

        result = subprocess.run(
            ["swift", str(COMPILER), str(path)],
            capture_output=True,
            text=True
        )

        output = result.stdout

        if (
            result.returncode != 0
            or "Pipeline compilation: SUCCESS" not in output
        ):
            print("COMPILATION FAILED")
            print(output)
            print(result.stderr)
            failed = True
            continue

        match = re.search(
            r"^\s*Buffer \d+: Globals\s*$"
            r"(.*?)"
            r"(?=^\s*Buffer \d+:|\Z)",
            output,
            re.M | re.S
        )

        if not match:
            print("Globals reflection unavailable")
            failed = True
            continue

        block = match.group(1)

        size_match = re.search(r"Size:\s*(\d+)", block)
        actual_size = int(size_match.group(1))

        actual = {
            field: int(pos)
            for field, pos in re.findall(
                r"(\w+): offset (\d+)", block
            )
        }

        print(f"Buffer size: {actual_size} / {expected_size}")

        valid = actual_size == expected_size
        checked = 0

        for field, pos in actual.items():
            if field.startswith("hlslccPad_"):
                continue

            original_name = re.sub(
                r"^hlslcc_mtx\d+x\d+",
                "",
                field
            )

            wanted = expected.get(original_name)

            if wanted is None:
                print("UNKNOWN FIELD:", field)
                valid = False
                continue

            ok = pos == wanted
            checked += 1
            valid &= ok

            print(
                f"{field}: expected={wanted}, actual={pos}",
                "OK" if ok else "MISMATCH"
            )

        if not valid or not checked:
            print("LAYOUT VALIDATION FAILED")
            failed = True
            continue

        updated = raw[:offset] + candidate.encode() + b"\0"

        new["code"] = (
            list(updated)
            if isinstance(new["code"], list)
            else updated
        )

        fixed += 1
        print("LAYOUT VALIDATION PASSED")

print("\n=== RESULTS ===")
print("Validated kernels:", fixed)

if failed or fixed != 3:
    print("Validation incomplete. No bundle generated.")
    raise SystemExit(1)

if DEST.exists():
    raise RuntimeError(f"Output already exists: {DEST}")

obj.save_typetree(converted)

temp = DEST.with_suffix(".tmp")
temp.write_bytes(env.file.save(packer="lz4"))

check_env = UnityPy.load(str(temp))
_, check = find_blur(check_env)

if check != converted:
    raise RuntimeError("Serialization validation failed")

temp.rename(DEST)

print("ALL THREE KERNELS VALIDATED")
print("New bundle:", DEST)
