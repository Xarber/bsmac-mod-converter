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
  const server = service({
    configuration, origin: 'https://beatsaber.xcenter.it', storage,
    exchange: async (_, url, checks) => {
      assert.equal(url.searchParams.get('state'), checks.expectedState);
      assert.ok(checks.expectedNonce && checks.pkceCodeVerifier);
      return { claims: () => ({ iss: 'https://id.xcenter.it', sub: url.searchParams.get('code') }) };
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
    return 'Bearer ' + (await pickup.json()).session;
  }
  const authorization = await signIn('test proof', 'first-account');
  const url = '/api/backups/1.40.8';
  const upload = { method: 'PUT', headers: { authorization, 'If-Match': '"0"', 'Content-Type': 'application/zip' },
    body: Buffer.from([0x50, 0x4b, 0x03, 0x04, 0x0a]) };
  assert.equal((await request(url, upload)).status, 204);
  assert.equal((await request(url, upload)).status, 412);
  assert.equal((await request(url, {headers: {authorization}})).status, 200);
  const other = await signIn('other test proof', 'second-account');
  assert.equal((await request(url, {headers: {authorization: other}})).status, 404);
  assert.equal((await request('/api/account/logout', {method: 'POST', headers: {authorization}})).status, 204);
  assert.equal((await request(url, {headers: {authorization}})).status, 401);
});
