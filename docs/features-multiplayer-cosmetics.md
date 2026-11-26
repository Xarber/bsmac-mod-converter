# Multiplayer cosmetics and desktop keyboard

Multiplayer Cosmetics targets the PC build of Beat Saber 1.40.8 running in MoltenVR. It adds renderers only to the local client's representations of other players. It sends no cosmetic data to the server.

- Sabers use mesh renderers from the local CustomSabersLite selection when the local model is present. Gameplay tracking and stock trails remain managed by the game.
- Colored remote blocks use the selected CustomNotes arrow or dot mesh for their color. Bombs and slider-specific substitutions are outside the current implementation.
- Remote walls use the selected CustomWalls material, optional overlay and optional replacement mesh. The original wall is restored when its gameplay object returns to the pool. Custom wall shaders may differ from stock clipping and effects.
- While the game window has desktop focus and its keyboard is open, normal characters and Backspace go to the most recently enabled supported keyboard. Use the existing VR buttons to submit or cancel. Built-in keyboards and BSML modal keyboards have separate input adapters. No OS-wide keyboard access is used.

With Beat Saber closed, run `bash scripts/install-multiplayer-cosmetics.sh`. Use `--stage` to build and generate the install plan without modifying the game. The installer signs the app and prints a rollback receipt. See [BeatTogether rollback](features-beattogether.md) for rollback and re-signing.

The plugin compiled against the installed 1.40.8 assemblies. Multiplayer appearance, keyboard focus, and behavior during pooling and scene changes still need an in-game check. Verify with a remote player and a map with walls and both arrow and dot blocks. For keyboard input, pick an in-game search field, focus the game window on the Mac, type a few characters, press Backspace, and submit with the in-game keyboard.
