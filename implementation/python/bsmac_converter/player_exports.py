# SPDX-License-Identifier: AGPL-3.0-only
"""Project a Beat Saber save onto the private cross-app record fields."""
import json
import sys
from pathlib import Path

from .player_backup import game_version

COUNTERS = ("timePlayed", "playedLevelsCount", "cleardLevelsCount", "failedLevelsCount",
            "fullComboCount", "goodCutsCount", "badCutsCount", "missedCutsCount", "totalScore")
MODES = ("campaignOverallStatsData", "soloFreePlayOverallStatsData",
         "partyFreePlayOverallStatsData", "onlinePlayOverallStatsData")


def project(instance, player):
    path = player / "PlayerData.dat"
    if path.stat().st_size > 32 * 1024 * 1024:
        raise ValueError("Player save exceeds export limit")
    players = json.loads(path.read_bytes()).get("localPlayers", [])
    if len(players) != 1:
        raise ValueError("Expected one local player profile")
    profile = players[0]
    stats = profile.get("playerAllOverallStatsData", {})
    scores = []
    for record in profile.get("levelsStatsData", []):
        if record.get("validScore") is not True or not record.get("highScore"):
            continue
        scores.append({name: record[name] for name in (
            "levelId", "difficulty", "beatmapCharacteristicName", "highScore", "maxCombo", "fullCombo", "playCount"
        ) if name in record})
    scores.sort(key=lambda row: row["highScore"], reverse=True)
    plugins = instance / "Beat Saber.app/Contents/Resources/Plugins"
    return {
        "game_version": {"version": game_version(instance)},
        "installed_mods": {"plugins": sorted(path.name for path in plugins.glob("*.dll") if path.is_file())[:500]},
        "scores": {"entries": scores[:200], "truncated": len(scores) > 200},
        "game_stats": {"modes": {mode: {key: stats.get(mode, {})[key] for key in COUNTERS
                                       if key in stats.get(mode, {})} for mode in MODES}},
    }


if __name__ == "__main__":
    print(json.dumps(project(Path(sys.argv[1]), Path(sys.argv[2])), allow_nan=False))
