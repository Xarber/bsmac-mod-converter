# Active assignment

Created 2026-11-24. Order follows the resumed request, with independent staging while the game remains open. No AGENTS.md changes are authorized. Usage windows are unavailable through the exposed tools.

| ID | Request | State | Next action / dependencies |
| --- | --- | --- | --- |
| T01 | Recover handoff, local scripts and diagnostics | Verified | Previous chat and local diagnostics recovered; no previous suggested commands assumed executed. |
| T02 | Diagnose actual Vivify and mapping-mod level abort | In Progress | User additionally reproduced return-to-picker on maps requiring only Mapping Extensions, only Noodle Extensions, and only Chroma. Latest log shows NoodleExtensions/Chroma transpiler failures, plus duplicate bundles on retry. Inspect actual runtime IL and each failure before changing patches. |
| T03 | Validate original maps and Metal derivatives; narrow loader bridge | Queued | Verify original SHA/CRC and derivative SHA/CRC; preserve Info.dat and original bundles. |
| T04 | CustomWalls legacy structured buffer and final install | Queued | Validate kind 2, flags 0, t0 stride 56, both serialized binding tables; compile Metal before staging. |
| T05 | raz0r runtime test and broader shader libraries | Queued | Preserve VR-mode Platforms enum and safety checks. |
| T06 | Exact PC AudioLink release, dependencies, conversion and installation | In Progress | Upstream v1.1.1 has an explicit 1.40.8 asset; BSIPA ^4.2.2 and SiraUtil ^3.0.5. Inspect embedded Bundle2021. |
| T07 | Original extensions in private bsmac-mod-converter repository | In Progress | Exclude MoltenVR bytecode/native translator, extracted assets, decompiled sources and personal logs. AGPL-3.0-only for original code. |
| T08 | CLI, cache, automatic conversion, rollback, context and regression tests | Queued | Build around existing local translator; no proprietary implementation redistribution. |
| T09 | Backed-up installation, signing, native startup and gameplay evidence | Queued | User authorized close/relaunch. Headset visual verification requires real runtime evidence. |
| T10 | Camera2 should show the user's actual in-game avatar | Queued | Added during execution. Inspect Camera2/avatar plugins and configuration; preserve custom model preferences. |
| T11 | Find macOS/MoltenVR alternative to Amethyst for Xbox One Kinect full-body tracking | Queued | Added during execution. Research actual hardware and tracker protocol support; no new toolchain or unrelated infrastructure installation requested. |

Commit references and verification outcomes will be recorded as operations complete. No release or public publication requested.
