// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { appFiles } from './app-files.mjs';

test('writes browseable chunks to the delegated application folder', async () => {
  const id = '01234567-89ab-cdef-0123-456789abcdef';
  const calls = [];
  const files = appFiles('user-token', 'xid-test', async (url, init) => {
    assert.equal(init.headers.Authorization, 'Bearer user-token');
    assert.ok(url.startsWith('https://id.xcenter.it/v1/apps/xid-test/files'));
    calls.push([init.method, init.body && JSON.parse(init.body)]);
    return Response.json({id, chunkSize: 3});
  });
  assert.equal(await files.save('backup.zip', Buffer.from('abcde')), id);
  assert.deepEqual(calls, [
    ['POST', {name: 'backup.zip', size: 5, browseable: true, editable: true}],
    ['PUT', {position: 0, content: 'YWJj'}],
    ['PUT', {position: 3, content: 'ZGU='}],
    ['POST', {}],
  ]);
});
