# Verification checkpoint — 2026-11-24

Development source; no release or deployment. Evidence is scoped to the exact operations below. Installation and actual headset gameplay are not yet verified.

- Recovered previous chat and local diagnostic files. User confirmed previous suggested final commands had not been run.
- Verified installed Vivify DepthBlit resource CRC 2484130083 and its matching IL constant.
- Revalidated both original map SHA-256 and Info.dat CRC, and both converted bundle SHA-256/CRC, with no map modifications.
- AudioLink upstream asset/embedded manifest confirms 1.1.1+1.40.8, game 1.40.8, BSIPA ^4.2.2, SiraUtil ^3.0.5. Recorded installed versions satisfy these ranges. One shader, 28 translated Metal variants compiled on Apple M4. Original CRC 2240245297, derivative 3530250200.
- Final CustomWalls structured-buffer bundle converted: one shader, 80 variants compiled on Apple M4. Kind-2 flags 0 retained; embedded and serialized buffer bindings validated.
- Reopened original and derivative bundles; original Direct3D programs and non-shader asset bytes preserved in the unified pipeline integration for AudioLink and CustomWalls.
- Initial synthetic test run: 17 passed. Overall Python coverage 27%, including backend-dependent legacy code not exercised by synthetic fixtures. This does not meet a complete changed-code coverage claim; additional integration evidence is required.
- Managed helper compilation succeeded after fixing paths following folder reorganization. The initial reorganization interrupted a running tool build; it was rerun from implementation/. NativeDiagnostics has a framework assembly-version warning that requires runtime validation.
- One Chroma diagnostic repack initially failed because the new patch tool lacked a dependency resolver. Input files were untouched; resolver search paths were added for retry.

Known active blockers: mapping-mod gameplay aborts; actual live Harmony instruction streams needed. Initial logs include NoodleExtensions/Chroma failed match operations; later user tests also show NoodlePlayerTransformManager invalid casts. Repeated retries can leak loaded map bundles. Chroma's missing Windows LookupID.dll has a managed fallback and is not by itself proof of the abort cause. Audio-reactive output, procedural wall appearance, raz0r runtime selection and physical Quest 3 behavior remain unverified.

Compliance infrastructure is structural and scoped. Most functional requirement states remain Pending. No full application audit, security certification, complete UADS conformance, or native release success is claimed.

## Runtime and source checkpoint

- Public repository authorized by user and visibility verified through GitHub API. Initial commit d50e0f8 pushed to testing. beta/main were initialized as repository seed branches; no release workflows or releases were run. Hardware SSH signing failed with “device not found”; commit was created unsigned without changing signing identity.
- Installed eight backed-up files and successfully verified the app's ad-hoc signature. Local receipt: instance `Compatibility-Rollback/20261124T194026223521Z/receipt.json`. AudioLink, CustomWalls, Vivify, diagnostic-only Noodle/Chroma, bridge/diagnostic libraries and raz0r library are included. No map originals were replaced.
- Relaunched through MoltenVR; AudioLink manifest and app/menu installers are recognized. CustomWalls reports five walls loaded. AssetBundleLoadingTools loads six manifests/644 shaders, but this does not establish successful raz0r replacement or complete Metal support.
- Runtime Vivify log explicitly reports original/derivative hash validation and “Metal bundle loaded”. Later initialization still aborts in gameplay patches.
- Runtime instructions show Mapping Extensions already inserts a clamp before Noodle's strict original match. Narrow replacements were prepared for Noodle time-row/obstacle matching and Chroma debris matching. Their initial compilation failed because netstandard reference assemblies did not expose the Mono mscorlib Label type. Using the installed game's mscorlib/System/System.Core references fixed compilation.
- Synthetic compatibility regressions executed inside the installed Mono runtime: PASS for unmodified/already-clamped time rows, actual obstacle local, debris delegate replacement and unknown-shape refusal. Initial standalone Mono execution lacked System.Native mapping; adding the same installed libmono-native mapping fixed the runner. This is a real Mono test, not game/headset verification.
- Initial GitHub quality run 45472973930 did not execute any steps: GitHub reports account payment/spending restriction. A rerun after user-authorized public visibility is being checked separately.

## Latest active results

- Rerun of quality workflow 45472973930 succeeded after the repository became public, executing installation, manifest validation, Python compilation, 17 tests, coverage reporting, diff checks and source boundary validation. This validates initial source commit d50e0f8 only.
- User reports The Wall, Drowned Rotten and Alone, Ignite, you, and requirement-free Color all return to the picker; vanilla $100 Bills works. The current log no longer shows the repaired Noodle/Chroma transpiler exceptions in these attempts. Shared custom-song loading remains unresolved.
- Malformed file URLs in logs encode spaces as plus signs. MediaAsyncLoader delegates both audio and artwork paths to FileHelpers.GetEscapedURLForFilePath. The owning helper is being inspected; no global networking bypass is planned.
- User reports MysticalSnowWalls crashes and PixelWalls renders with pixelated/censor-like appearance. The named MysticalSnowWalls resource contains .HToga/Spawn Holo (Optimized), not the remaining procedural-particle shader. Do not attribute the Snow crash to structured buffers without new evidence. No native macOS Beat Saber crash report was available at inspection.
- Runtime copies of both verified Vivify derivatives were cloned into Application Support/cache/maps, hashes checked and the allowlist atomically regenerated. The bridge no longer needs Desktop access for those map bundles.
- Free built-in NativeAvatarRenderer compiled successfully with no warnings. Not yet installed or runtime verified. The user explicitly declined paid Naluluna packages and wants the existing game avatar.
- Broader library conversion exposed Unity shader version 201802150. Compatibility was prepared but integration is blocked by free space: the guard refused new conversion below 2300 MiB. About 631 MiB was available at the last check. No source, dependencies, backups or user data were deleted.

## Custom-media and Desktop follow-up

User reported all five custom tests return, including requirement-free Color; vanilla $100 Bills works. Actual MediaLoader/BGLib helper inspection confirmed form-encoded file URLs, matching curl failures with plus signs in local paths. Original LocalMediaBridge encodes each path segment with percent escapes, preserves existing URLs, and is linked only into the observed FileHelpers method after signature/body checks. Tests executed in installed game Mono pass for spaces, literal plus/hash/percent, Unicode roundtrip and remote URL passthrough, alongside existing compatibility fixtures. Installed under transaction 20261124T201937894623Z with built-in avatar renderer. Runtime success remains pending.

Two validated Vivify derivatives were cloned to Application Support cache/maps and allowlist paths regenerated, removing this loader's dependency on Desktop access. No macOS privacy settings were bypassed. Original map assets were retained.

NativeAvatarRenderer compiles against installed 1.40.8 assemblies with zero warnings/errors; no runtime or headset appearance verification yet. MysticalSnowWalls remains unsafe to retest; its own shader differs from the final structured-buffer resource. Broader conversion stopped at the disk guard; approximately 533 MiB free observed. Only verified generated MSL cache was archived (about 19 MB recovered), no unrelated data removed.

Post-install codesign --verify --deep --strict completed with exit code 0. The game remained closed. This verifies bundle signing, not gameplay.

Latest local checks: 17 Python tests passed with explicit implementation/python module path; schema/registry/evidence validator passed. An initial test invocation omitted that module path and failed collection (ModuleNotFoundError); corrected invocation passed. Commit 61df0ab was pushed and remote testing SHA verified. An overlapping earlier push caused one ref-lock rejection; sequential retry succeeded without force or history rewriting.

## November 25 conversion checkpoint

On the Apple M4 Mac, all 11 live original and derivative pairs matched their metadata CRC, registered derivative CRC and SHA-256. Breezer matched 1614546449 and 1762283956. No live row was changed. The UnityFS raw LZMA decoder uses the declared size because these blocks can omit an end marker. A trial reading one extra byte failed on Breezer and was corrected before the full successful check.

After applying Unity 2019 kind-2 support, the original Ego Death converted with 11 shaders and 226 variants passing native Metal compilation. The new 42-flux experimental candidate passed native Metal compilation of its emitted sources. It included 620 translated jobs, 4 failed jobs and 28 unsupported geometry programs across two shader families. `Particles/Standard Unlit` accounted for 56 successful jobs with no failures. Four other jobs were refused because the local backend reuses the same blob indices for distinct generated Metal code. The candidate was not registered. No visual parity or runtime pipeline creation was established.

The inspected Vivify downloader writes the original before marking the download finished. It currently returns immediately for an existing bundle. Those two locations are the relevant entry points for T14. No live game patch was installed and no game session was started for this batch.

## Automatic Vivify preparation checkpoint

Installed an application-associated Python/Metal worker and a scoped Vivify 1.0.5 preparation hook on the Apple M4 Mac. The project compiled both managed tools with zero warnings. The staged assembly reopened and contains the expected existing-bundle, download completion and waiter calls. The existing checked Vivify bridge call and the complete DepthBlit resource were retained. The worker converted Ego Death from scratch into an isolated test index using native Metal compilation. The next rescan returned all 11 registered entries. A smoke test running on installed game Mono launched the persistent worker through the actual managed process bridge, including a path with spaces, and propagated a deliberately wrong CRC error. Twenty-five Python tests passed. App signing and deep strict verification passed. The live index's SHA-256 remained identical. Receipt `20261125T060312425682Z` backs up both assemblies. No game launch occurred and the actual Vivify screen, first play after download and controller cancellation still need user testing.

## Loose equipment preparation checkpoint

Built and reopened the exact installed CustomNotes, CustomWalls and CustomSabersLite patch targets. Four local files were backed up and installed as one transaction, receipt `20261125T062031452982Z`. App signature and automatic marker checks passed. A separate index rehearsal converted actual original equipment samples: 52 note, 80 wall and 8 saber programs, all native Metal compilation passes. Existing current equipment was checked for unnecessary replacement. The installed Mono harness passed on retry for compatible Plasma Notes passthrough, source and derivative hash selection, and rejection of a bad derivative hash. The initial standalone harness process exited 137 with a macOS taskgated invalid-signature report. Its direct rerun passed. Twenty-six Python tests and the structural compliance check passed. The Vivify index checksum remained identical. Actual Unity loading, watcher events in a game session and visual behavior still require user testing.

SiraUtil 3.2.1 was already present, recognized by IPA and configured with the G key. Its settings installer selects a no-op FPFC service without the explicit `fpfc` argument, and its stock mode transition cycles the XR loader. Camera2 also expects a legacy SimpleCameraController update hook. The user prioritizes automatic conversion first. No SiraUtil or Camera2 files were changed in this batch.

A later user-started game session resolved the updated bridge and logged all nine installed custom notes loaded, including successful Plasma Notes adaptation. The game had zero loose walls to exercise. Fresh equipment conversion and appearance were not observed. Deep signature verification passed before launch. During the running session it reported mutable log and UserData resources added or modified by the game; no compatibility assembly was reported changed. No attempt was made to re-sign the running game.

During the same user-started session, **Final Slaughter** exercised an uncached Vivify bundle. Preparation began at 07:23:00 Rome time, completed at 07:23:16, and the checked loader reported the Metal bundle loaded at 07:23:18. The live index increased from 11 to 12 entries. The new receipt records 44 shaders and 226 translated programs with zero failed, unsupported or skipped jobs. Vivify then logged prefab assignments, render texture creation, and timed effects during gameplay. Visual parity and the missing-file download and cancellation branches remain unverified.

## BeatLeader replay checkpoint

The installed PC BeatLeader 0.9.33 assembly contains its replay player, download request, per-score on-disk cache and in-game save toggle. Inspection identified Windows path literals in the two replay directories. The guarded macOS path fix compiled and staged with an unchanged embedded manifest. The standalone MIT downloader at revision b6942b8102a7fb66b4d00ca4a8416ee484fc212d ran from Application Support and downloaded a 1,356,538-byte public replay with the expected BSOR v1 signature. Downloaded replay files and user configuration were kept local. Installed after the game had closed, backed up under receipt 20261125T063018371000Z and verified with deep strict code signature validation. EnableReplayCaching is true and SaveLocalReplays remained true. Both save directories exist. Replay playback and cache persistence still need runtime verification.

## Second handoff results

- Replay source and installation evidence were confirmed on remote `testing` through `bf4deea`. In-game replay caching is enabled, and the standalone downloader smoke test passed on a public BSOR. Playback still requires a game test.
- Backed up and hashed working Plasma bridge, CustomNotes, BeatLeader and the live map index. The installed Plasma DLL matched the separate session's output byte-for-byte. Built the consolidated bridge with the prior System.ValueTuple warning and zero errors.
- Qosmetics tests used actual local Plasma and True Acc packages with both PC and Android entries. Native Metal passed for 4 and 18 translated programs across the two models. A second pass reused both derivatives. Both original packages were preserved. Installed the generic bridge and worker, and added True Acc for gameplay validation under receipt 20261125T151418478483Z. Strict signature verification completed.
- Multiplayer+ 6.4.2 logs repeatedly showed a `NullReferenceException` when opening the flow coordinator. Upstream 6.4.5 for 1.40.0–1.40.8 removes the old early multiplexer call in `Present`. Checked the installed suite's dependency versions and installed the three already-present modules as a matched update. Strict signature verification passed. Real lobby and match behavior were not observed.
- BeatTogether 2.2.1 obtained authorization before the transport failed on `libcrypto-1_1-x64`. The local game transport also requires version 132103 and `enet_host_create_ssl`. The upstream generic macOS ENet source inspected lacks that entry point. No transport or server settings were changed.
- Modern 42-flux candidate used separate Metal indices for distinct `(program, parameter block)` jobs. Native Metal passed for all emitted sources, 624 translated jobs across 74 shaders, zero failed jobs. Offline inspection found 76 D3D shaders and 642 Metal references across the 74 supported shaders. Geometry-dependent Vilar/GeometryDissolve and Vilar/GeometryDissolveOutline remain unsupported. No live map was replaced.

### Live render diagnostics preparation

The renderer observer was compiled without warnings and installed under receipt `20261125T151850307320Z`. The local marker enables three bounded samples per successfully loaded Vivify derivative, reporting unsupported or internal-error shaders with renderer hierarchy, material slot, pass count and keywords. An optional shader-name substring can include a known suspect even when Unity reports it supported. It observes the current scene and identifies the loaded bundle context, without claiming every renderer came from that bundle or that the actual GPU variant is known. No shader or material was changed. Live measurements require a user-owned gameplay run.

Read-only auditing of the currently registered experimental copies found no Metal program references for the two Vilar shaders in 42-flux, Bytesized/Grass in Lifelike, Roystan/Grass in Yoi Okashi to Warui Okashi, and AshleyScarlet/ParticleScreen/Visualizer in Through The Screen. This identifies candidates and does not establish which objects actually appeared magenta.

Final handoff check found 11 local BSOR recordings and 11 cached downloads in the corrected BeatLeader directories. Playback appearance was not observed. The live automatic worker is enabled, the app signature verifies and the map index still has 12 registrations. All 29 local Python tests passed, and GitHub source checks passed at ba8c28e and 0e5cca6.

## Installer replication checks

The ported installer resolved and verified 44 official BeatMods dependency archives for the pinned 1.40.8 profile. This was a cache-only check. The installation command was not run on the existing modded game. AssemblyTool built without warnings, and the Qosmetics hook staged and reopened against the backed-up upstream CustomNotes DLL. The ReeSabers ports were run on the local original: 31 shaders, 4,932 translated variants and 12 compute kernels reported with zero conversion failures. Metal then successfully built all three patched blur compute pipelines and verified their reflected layouts against the original. Thirty Python tests passed. Full clean-instance installation and gameplay remain unverified.


## Fresh compatibility staging rehearsal

On Apple Silicon, built AssemblyTool and five bridges against an isolated reference fixture. The fixture included original pinned BeatMods DLLs, upstream AudioLink and BeatLeader, and the original BGLib managed DLL. Rebuilt the Vivify, AudioLink, BeatLeader and four CustomWalls embedded bundles and staged the managed hooks. An initial rehearsal stopped at the BeatLeader patch because the intermediate DLL was outside the game dependency folders. The corrected rehearsal resolved dependencies from the supplied game resources and produced the install plan. QosmeticsBridge emitted a System.ValueTuple reference-version warning and built successfully. This verified staging with a populated converter cache. It did not install a new game instance or launch it.
