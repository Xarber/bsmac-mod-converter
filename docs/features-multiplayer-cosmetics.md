# Multiplayer cosmetics and desktop keyboard

Multiplayer Cosmetics targets the PC build of Beat Saber 1.40.8 running in MoltenVR. It adds renderers only to the local client's representations of other players. It sends no cosmetic data to the server.

- Sabers use mesh renderers from the local CustomSabersLite selection when the local model is present. Gameplay tracking and stock trails remain managed by the game.
- Colored remote blocks use the selected CustomNotes arrow or dot mesh for their color. Bombs and slider-specific substitutions are outside the current implementation.
- Remote walls use the selected CustomWalls material, optional overlay and optional replacement mesh. The original wall is restored when its gameplay object returns to the pool. Custom wall shaders may differ from stock clipping and effects.
- While the game window has desktop focus and its keyboard is open, normal characters and Backspace go to the most recently enabled supported keyboard. Use the existing VR buttons to submit or cancel. Built-in keyboards and BSML modal keyboards have separate input adapters. No OS-wide keyboard access is used.

With Beat Saber closed, run `bash scripts/install-multiplayer-cosmetics.sh`. Use `--stage` to build and generate the install plan without modifying the game. The installer signs the app and prints a rollback receipt. See [BeatTogether rollback](features-beattogether.md) for rollback and re-signing.

The plugin compiled against the installed 1.40.8 assemblies. Multiplayer appearance, keyboard focus, and behavior during pooling and scene changes still need an in-game check. Verify with a remote player and a map with walls and both arrow and dot blocks. For keyboard input, pick an in-game search field, focus the game window on the Mac, type a few characters, press Backspace, and submit with the in-game keyboard.

The first installed build was skipped by BSIPA because its embedded manifest and entry-point namespaces differed. The installer now checks the compiled entry point before applying it. A successful startup logs `Multiplayer Cosmetics startup completed.`

Remote arrow and dot blocks use the same 0.4 baseline and note-size setting as CustomNotes and inherit the remote note's visual transform. They use the active note color and the model's color intensity, honoring renderers marked to keep their original color. The cloned visual hierarchy retains Unity Animator and Animation components. Gameplay scripts and physics components are stripped from the clone, so script-driven effects may differ. Runtime size, tint, rotation, and animation are pending a headset retest.

Remote saber meshes use the remote player's left and right note palette from their gameplay color manager. The plugin applies renderer-local overrides only to material slots CustomSabersLite designates as colorable. It retains the selected model and fixed-color details. In-game comparison against a player with a different palette remains pending.

Version 0.2 adds separate saved toggles under Mod Settings → Multiplayer Cosmetics. Remote cosmetic changes apply when the next multiplayer scene loads. Physical keyboard input can be disabled independently. On Windows the bridge receives focused keyboard input through Unity Input System, with legacy input available when Unity exposes no current keyboard device.

For Windows PCVR 1.40.8, install BSIPA and Beat Saber Markup Language 1.12.5 or a compatible version. Replace `Plugins/MultiplayerCosmetics.dll` next to `Beat Saber.exe`. Keep the matching cosmetics mods for the models you want to use. CustomSabersLite is now captured at its own initialization hook as well as the game container hook. Windows playback and settings persistence still need a live retest.
