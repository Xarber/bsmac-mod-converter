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
