// SPDX-License-Identifier: AGPL-3.0-only
import { createHash } from 'node:crypto';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { once } from 'node:events';
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Configuration } from 'openid-client';
import { service } from './server.mjs';

test('single-use sign-in pickup and account-scoped revision-guarded backup', async (t) => {
  const storage = await mkdtemp(join(tmpdir(), 'bsmac-sync-'));
  const configuration = new Configuration({
    issuer: 'https://id.xcenter.it', authorization_endpoint: 'https://id.xcenter.it/auth',
  }, 'test-client');
  let renewals = 0;
  const accounts = new Map();
  const files = (access) => {
    if (!accounts.has(access)) accounts.set(access, new Map());
    const entries = accounts.get(access);
    return {
      list: async () => [...entries.keys()].map(name => ({name, id: name, created: Date.now()})),
      read: async (id) => entries.get(id),
      save: async (name, data) => entries.set(name, data),
      remove: async (id) => entries.delete(id),
    };
  };
  const server = service({
    configuration, files, legacyFiles: () => ({ list: async () => [] }), origin: 'https://beatsaber.xcenter.it', storage,
    refresh: async (_, token) => {
      assert.equal(token, "original-test-refresh");
      renewals++;
      return { refresh_token: "rotated-test-refresh", access_token: "first-account", expires_in: 600 };
    },
    exchange: async (_, url, checks) => {
      assert.equal(url.searchParams.get('state'), checks.expectedState);
      assert.ok(checks.expectedNonce && checks.pkceCodeVerifier);
      const subject = url.searchParams.get('code');
      return { access_token: subject, claims: () => ({ iss: 'https://id.xcenter.it', sub: subject }),
        ...(subject === 'first-account' ? { refresh_token: 'original-test-refresh', expires_in: 1 } : {}) };
    },
  });
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  t.after(async () => {
    await new Promise((resolve) => server.close(resolve));
    await rm(storage, { recursive: true, force: true });
  });
  const request = (route, options) => fetch('http://127.0.0.1:' + server.address().port + route, options);
  const post = (route, body) => request(route, { method: 'POST', body: JSON.stringify(body) });
  async function signIn(proof, account) {
    const start = await post('/api/account/start', { challenge: createHash('sha256').update(proof).digest('hex') });
    assert.equal(start.status, 201);
    const { handle, authorization } = await start.json();
    const state = new URL(authorization).searchParams.get('state');
    assert.equal((await post('/api/account/pickup', {handle, proof: 'wrong'})).status, 401);
    assert.equal((await post('/api/account/pickup', {handle, proof})).status, 202);
    const callback = '/api/account/callback?' + new URLSearchParams({state, code: account});
    assert.equal((await request(callback)).status, 200);
    assert.equal((await request(callback)).status, 400);
    const pickup = await post('/api/account/pickup', {handle, proof});
    assert.equal(pickup.status, 200);
    assert.equal((await post('/api/account/pickup', {handle, proof})).status, 401);
    const result = await pickup.json();
    assert.equal(result.persistent, account === 'first-account');
    assert.equal(result.expiresIn, result.persistent ? 14 * 24 * 60 * 60 : 28800);
    return 'Bearer ' + result.session;
  }
  const authorization = await signIn('test proof', 'first-account');
  const url = '/api/backups/1.40.8';
  const upload = { method: 'PUT', headers: { authorization, 'If-Match': '"0"', 'Content-Type': 'application/zip' },
    body: Buffer.from([0x50, 0x4b, 0x03, 0x04, 0x0a]) };
  assert.equal((await request(url, upload)).status, 204);
  assert.equal((await request(url, upload)).status, 412);
  assert.equal(renewals, 1);
  assert.equal((await request(url, {headers: {authorization}})).status, 200);
  const snapshot = '1.40.8-manual-20261126123456789-abcdef12';
  assert.equal((await request('/api/backups/' + snapshot, upload)).status, 204);
  const list = await request('/api/backups', {headers: {authorization}});
  assert.deepEqual((await list.json()).versions.map(row => row.version).sort(), ['1.40.8', snapshot]);
  const head = await request('/api/backups/' + snapshot, {method: 'HEAD', headers: {authorization}});
  assert.equal((await request('/api/backups/' + snapshot, {method: 'DELETE',
    headers: {authorization, 'If-Match': head.headers.get('etag')}})).status, 204);
  assert.equal((await request(url, {headers: {authorization}})).status, 200);
  const other = await signIn('other test proof', 'second-account');
  assert.equal((await request(url, {headers: {authorization: other}})).status, 404);
  assert.equal((await request('/api/account/logout', {method: 'POST', headers: {authorization}})).status, 204);
  assert.equal((await request(url, {headers: {authorization}})).status, 401);
});
