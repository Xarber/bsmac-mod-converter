# Architecture

`implementation/python/bsmac_converter` separates UnityFS CRC validation (`crc`), immutable-map allowlists (`index`), original legacy shader extensions (`legacy`, `structured`), content-addressed conversion (`pipeline`, `worker`), file transactions (`transaction`) and command orchestration (`cli`).

The backend is supplied separately at a configured local path. Only public-facing Python interfaces are called; no bytecode or native binaries are copied into this repository. UnityPy 1.25.4 is pinned for serialized typetree compatibility. Original Direct3D bytecode and non-shader objects are checked after rewriting. New Metal shader source is compiled through the actual local GPU driver. Compilation proves syntax and platform acceptance, not visual correctness.

Legacy structured-buffer resources are temporarily normalized only for parsing. Their real kind-2 records and flags remain in the output. DXBC declarations supply resource type and stride; Metal slots are allocated without collision and synchronized with Unity's m_BufferParams. Unrecognized resource formats fail closed.

`implementation/tools/AssemblyTool` performs narrowly selected managed IL operations and reopens staged assemblies. `VivifyMetalBridge` intercepts exactly one checked Vivify map-load call and validates a five-field allowlist. `NativeDiagnostics` records actual incoming Harmony instruction streams without disabling or bypassing failing patches. `compile-metal.swift` validates translated source with Metal.

File installation is explicit and separate from conversion. Backups and receipts track old/new SHA-256. Rollback refuses to overwrite files changed after installation. App signing and runtime startup are separate operations whose observed outcomes must be recorded.

Cache keys include original content, separately installed backend bytes, extension source and UnityPy version. Automatic discovery stages supported map/model/library files. It does not automatically rewrite arbitrary plugin DLLs. Private original assets, diagnostics and conversion outputs remain outside version control.

The Unity 2021 adapter uses the local backend's job layout and applies the shared unsigned-offset repair before serialization. Its translation worker retains program and parameter bytes and checks their Metal table indices. Kind-2 bindings share the DXBC reconstruction and collision checks used by the Unity 2018 and 2019 implementations. Non-vertex/fragment stages are explicitly counted. Strict conversion declines incomplete outputs. Explicit experimental conversion stages verified bytes with a per-shader report and never auto-registers them.

Index publication uses an exclusive lock and an atomic write, retaining existing records outside the current conversion directory. Regular automatic CLI registration does not replace an existing selection for the source hash and CRC. Manual `index` reconstruction may update records that it revalidates. Current `watch` is polling and does not gate Vivify playback.

Vivify's on-demand engine lives in `engine.py` and `AutomaticPreparation.cs`. The exact versioned patch wires its existing-bundle branch and download completion into the same off-thread request. An isolated application-owned Python environment uses the locally compiled Metal validator. The worker has no system daemon and leaves the existing integrity bridge in place. See [Automatic Vivify preparation](features-automatic-vivify.md).

Loose models use a separate verified index and exact mod loader adapters. Notes use game-owned filesystem notifications and cached reads. Loose wall and saber menus await the worker at their existing async load call. Embedded and stream loaders remain independent. See [automatic equipment](features-automatic-equipment.md).
