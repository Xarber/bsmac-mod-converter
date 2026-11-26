// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {test} from 'node:test';
import {writeSharedRecords} from './shared-records.mjs';

test('writes each collection privately with user consent and revision checks', async () => {
  const input = {game_version: {version: '1.40.8'}, installed_mods: {plugins: ['Example.dll']},
    scores: {entries: [{levelId: 'custom_level_example', highScore: 42}], truncated: false},
    game_stats: {modes: {soloFreePlayOverallStatsData: {timePlayed: 120}}}};
  const saved = [];
  const provider = async (url, init) => {
    assert.equal(init.headers.Authorization, 'Bearer user-token');
    assert.match(url, /^https:\/\/id\.xcenter\.it\/v1\/apps\/test-app\/data\//);
    if (!init.method) return Response.json({revision: 7});
    assert.equal(init.headers['If-Match'], '"7"');
    assert.equal(JSON.parse(init.body).public, false);
    saved.push(url.split('/').at(-2));
    return new Response(null, {status: 204});
  };
  await writeSharedRecords(input, 'user-token', 'test-app', provider);
  assert.deepEqual(saved, Object.keys(input));
  input.scores.entries[0].playerName = 'excluded';
  await assert.rejects(writeSharedRecords(input, 'user-token', 'test-app', provider), {status: 400});
});
