# BeatLeader replays on native macOS

The installed BeatLeader PC 0.9.33 assembly includes its own replay player, a leaderboard replay downloader, and a local save toggle. Open a score on the BeatLeader leaderboard and select its replay play control. BeatLeader fetches the replay if it is not cached. Enable **Store locally** in the replay options to retain downloaded copies. The configuration key is `EnableReplayCaching`. Replay recording is separately controlled by `SaveLocalReplays`.

`AssemblyTool beatleader-replay-paths` targets the inspected unsigned version. It sets BeatLeader's recordings and download cache to `UserData/BeatLeader/Replays/` and `UserData/BeatLeader/ReplayerCache/` through IPA's UserData root. It preserves the assembly's manifest, shaders, playlists and replay decoding. Stage the file separately, keep a rollback receipt and verify signatures while the game is closed. In-game playback and saving require a real game session to verify.

## Standalone downloader

[BeatLeader Replay Downloader](https://github.com/NotNic182/beatleader-replay-downloader) is a separate MIT-licensed utility installed locally at upstream revision `b6942b8102a7fb66b4d00ca4a8416ee484fc212d`. It runs in its own Python 3.12 virtual environment under `~/Library/Application Support/bsmac/replay-downloader`. This directory contains the upstream license and revision record. It is not part of the game installation and is not included in Git.

```sh
replay_dl="$HOME/Library/Application Support/bsmac/replay-downloader/beatleader-replay-downloader"
"$replay_dl" --help
"$replay_dl" PLAYER_ID --replays --recent 5 --no-convert --output /path/to/replays
```

The standalone tool downloads public data you select. Local replays, downloaded player files, logs, Beat Saber binaries and user-specific configuration stay out of source control. A real public BSOR v1 sample passed download and header verification. The game does not need to be running for the standalone downloader.
