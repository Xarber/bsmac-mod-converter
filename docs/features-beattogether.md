# BeatTogether on Apple Silicon

Beat Saber 1.40.8 with BeatTogether 2.2.1 uses the game's Ignorance assembly. On macOS, its Windows library names caused initialization to fail before it could start the ENet transport.

With BeatTogether and its dependencies installed, close the game and run `bash scripts/install-beattogether.sh --install` from this repository. The script requires Apple Silicon, the Apple command-line compiler, Homebrew OpenSSL 3, and Python 3.12 or later. It defaults to the Player Backup Python environment. Set `BSMAC_PYTHON` to another compatible interpreter or `BSMAC_OPENSSL_PREFIX` to another arm64 OpenSSL 3 installation. Run without `--install` to stage and validate the native build.

The installer verifies the Ignorance assembly hash, fetches ENet-CSharp v2.4.7 at commit `dd97d94787994a67263ac8ecadd19119e1da8c8c`, and builds a dedicated arm64 library. The Darwin receive patch normalizes IPv4 peers returned by a dual-stack socket. A Mono configuration next to Ignorance maps its three native imports. The installer records a rollback receipt and verifies the re-signed app. To undo, close the game, run `bsmac-mod-converter rollback RECEIPT` with the printed path, then re-sign the app with `codesign --force --deep --sign - APP_PATH`.

The official BeatTogether server advertises plain ENet. The adapter accepts plain ENet and explicitly rejects TLS modes. Existing server settings control the selected mode. Test the final connection by launching through MoltenVR and creating or joining a BeatTogether lobby.

## Verification

On an Apple Silicon Mac, a separate test process loaded the 1.40.8 Unity Mono runtime, the installed Ignorance assembly and its installed library mappings, then exchanged a reliable payload over both IPv4 and IPv6 loopback. The installer confirmed both TLS modes were rejected and the application signature passed strict verification. A live lobby and matchmaking remain unverified.
