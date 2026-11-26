// SPDX-License-Identifier: AGPL-3.0-only
const number = value => typeof value === 'number' && Number.isFinite(value) && value >= 0;
const string = value => typeof value === 'string' && value.length <= 180;
const boolean = value => typeof value === 'boolean';
const counters = ['timePlayed', 'playedLevelsCount', 'cleardLevelsCount', 'failedLevelsCount',
  'fullComboCount', 'goodCutsCount', 'badCutsCount', 'missedCutsCount', 'totalScore'];
const modes = ['campaignOverallStatsData', 'soloFreePlayOverallStatsData',
  'partyFreePlayOverallStatsData', 'onlinePlayOverallStatsData'];
const shape = (object, fields) => object && typeof object === 'object' && !Array.isArray(object) &&
  Object.entries(object).every(([key, value]) => Object.hasOwn(fields, key) && fields[key](value));
const array = (limit, validate) => value => Array.isArray(value) && value.length <= limit && value.every(validate);
const score = value => shape(value, {levelId: string, difficulty: number, beatmapCharacteristicName: string,
  highScore: number, maxCombo: number, fullCombo: boolean, playCount: number});
const validators = {
  game_version: value => shape(value, {version: version => /^\d{1,5}\.\d{1,5}\.\d{1,5}$/.test(version) && string(version)}),
  installed_mods: value => shape(value, {plugins: array(500, name => string(name) && /^[^/\\]+\.dll$/.test(name))}),
  scores: value => shape(value, {entries: array(200, score), truncated: boolean}),
  game_stats: value => shape(value, {modes: data => shape(data, Object.fromEntries(modes.map(mode => [
    mode, fields => shape(fields, Object.fromEntries(counters.map(field => [field, number]))),
  ])))}),
};

export async function writeSharedRecords(input, access, client, request = fetch) {
  const invalid = () => Object.assign(new Error('Invalid shared records'), {status: 400});
  if (!shape(input, validators) || Object.keys(input).length !== Object.keys(validators).length) throw invalid();
  const records = Object.entries(input).map(([collection, data]) => {
    const payload = JSON.stringify({data, public: false});
    if (Buffer.byteLength(payload) > 60 * 1024) throw invalid();
    return [collection, payload];
  });
  for (const [collection, payload] of records) {
    const url = 'https://id.xcenter.it/v1/apps/' + encodeURIComponent(client) + '/data/' + collection + '/latest';
    const options = {redirect: 'error', signal: AbortSignal.timeout(10000), headers: {Authorization: 'Bearer ' + access}};
    const existing = await request(url, options);
    if (!existing.ok && existing.status !== 404)
      throw Object.assign(new Error('App data access failed'), {status: existing.status === 401 ? 403 : existing.status});
    const revision = existing.status === 404 ? 0 : (await existing.json()).revision;
    if (!Number.isSafeInteger(revision) || revision < 0) throw new Error('Invalid record revision');
    const saved = await request(url, {...options, method: 'PUT', body: payload,
      headers: {...options.headers, 'Content-Type': 'application/json', 'If-Match': '"' + revision + '"'}});
    if (!saved.ok) throw Object.assign(new Error('App data write failed'), {status: saved.status});
  }
}
