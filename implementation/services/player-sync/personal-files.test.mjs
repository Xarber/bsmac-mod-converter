// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { personalFiles, fileLimit } from './personal-files.mjs';

test('uploads into the signed-in user’s private Files folder', async () => {
  const requests = [];
  const files = personalFiles('delegated-token', async (url, request) => {
    assert.equal(new URL(url).origin, 'https://id.xcenter.it');
    assert.equal(request.headers.Authorization, 'Bearer delegated-token');
    requests.push([new URL(url).pathname, request.method, request.body && JSON.parse(request.body)]);
    if (request.method === 'GET') return Response.json({folders: []});
    return Response.json({id: 'personal-folder'}, {status: 201});
  });
  await files.save('1.40.8.zip', Buffer.from('backup'));
  assert.deepEqual(requests, [
    ['/v1/files', 'GET', undefined],
    ['/v1/files/folders', 'POST', {name: 'Beat Saber Player Backups'}],
    ['/v1/files', 'POST', {folder: 'personal-folder', name: '1.40.8.zip', content: 'YmFja3Vw'}],
  ]);
  await assert.rejects(files.save('large.zip', Buffer.alloc(fileLimit + 1)), {status: 413});
});
