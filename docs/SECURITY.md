# Security, provenance and licensing

Original code in this repository is AGPL-3.0-only. The GNU license text is in LICENSE. Local preexisting extension scripts were authored in the preceding user-directed compatibility session; only their original source functions are consolidated here. MoltenVR's supplied bytecode/native translator has no established redistribution grant in this task, so none is included. UnityPy, lz4, jsonschema and Mono.Cecil retain their own licenses. No game, mod or map assets or decompiled code may be committed.

AudioLink upstream is Aeroluna/BSAudioLink, MIT licensed. The inspected release v1.1.1 includes an explicit Beat Saber 1.40.8 artifact and embedded manifest. Local modified binaries remain local. Other third-party source must be reviewed before any redistribution.

Threat boundaries: downloaded assets and manifests are untrusted; local configuration and backups control file installation. Keep originals immutable. The Vivify allowlist uses full original and derivative SHA-256 plus independently validated Unity CRCs; no zero-CRC substitution or global hook is allowed. A modified or absent derivative retains checked original loading. SHA-256 identifies content, not publisher authenticity. Only obtain mod releases from verified upstream channels.

Archive repacking preserves every entry and the manifest byte-for-byte except the specific shaders.asset payload. It does not extract archive paths into arbitrary locations. Duplicate entries are rejected. Unsupported legacy metadata aborts conversion. Maintain minimum free-space checks and preserve existing working mods.

The diagnostics helper writes method instructions only to local converter cache. Private logs may contain identities elsewhere and must not be committed. No account system, public network service, telemetry or embedded credentials is introduced.

Report vulnerabilities privately to the repository owner. Preserve reproductions locally, assess affected versions and release impact, test the correction, then publish only through separately authorized channels.

The automatic worker accepts only a resolved `bundleWindows2021.vivify` under the configured CustomLevels root. The managed bridge launches a fixed local command with no shell and passes a bounded data request on standard input. Vivify retains all download and consent responsibility. The original CRC must agree with map metadata and any download CRC. Per-source file stamps and a final original check prevent publication after a changed download. Locks, bounded waits and a retry cooldown constrain overlapping or repeatedly failing conversion. UnityPy and the local translator still parse untrusted asset bytes in a local subprocess running with the user's access. The subprocess is a failure boundary and does not provide OS sandboxing.
