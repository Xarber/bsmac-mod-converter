// SPDX-License-Identifier: AGPL-3.0-only
export const fileLimit = 32 * 1024 * 1024;

export function appFiles(access, clientId, request = fetch) {
  const base = 'https://id.xcenter.it/v1/apps/' + encodeURIComponent(clientId) + '/files';

  async function call(route, method = 'GET', data) {
    if (!access) throw Object.assign(new Error('App data consent required'), { status: 403 });
    const response = await request(base + route, {
      method, redirect: 'error', signal: AbortSignal.timeout(20000),
      headers: { Authorization: 'Bearer ' + access, ...(data ? { 'Content-Type': 'application/json' } : {}) },
      body: data && JSON.stringify(data),
    });
    if (!response.ok) {
      const status = response.status === 401 ? 403 : ([403, 404, 409, 413].includes(response.status) ? response.status : 502);
      throw Object.assign(new Error('App Files request failed'), { status });
    }
    return response;
  }

  return {
    async list() {
      const files = [];
      const seen = new Set();
      let cursor;
      do {
        const page = await (await call(cursor ? '?cursor=' + encodeURIComponent(cursor) : '')).json();
        files.push(...page.files);
        cursor = page.nextCursor;
        if (cursor && seen.has(cursor)) throw new Error('Repeated Files cursor');
        if (cursor) seen.add(cursor);
      } while (cursor);
      return files;
    },
    async read(id) {
      const response = await call('/' + encodeURIComponent(id));
      const chunks = [];
      let length = 0;
      for await (const chunk of response.body) {
        length += chunk.length;
        if (length > fileLimit) throw Object.assign(new Error('Backup exceeds transfer limit'), {status: 413});
        chunks.push(chunk);
      }
      return Buffer.concat(chunks);
    },
    async save(name, bytes) {
      if (bytes.length > fileLimit) throw Object.assign(new Error('Backup exceeds transfer limit'), { status: 413 });
      const {id, chunkSize} = await (await call('/uploads', 'POST',
        {name, size: bytes.length, browseable: true})).json();
      if (typeof id !== 'string' || !/^[0-9a-f-]{36}$/.test(id) ||
          !Number.isSafeInteger(chunkSize) || chunkSize < 1 || chunkSize > 1024 * 1024)
        throw new Error('Unexpected upload metadata');
      const route = '/uploads/' + id;
      try {
        for (let position = 0; position < bytes.length; position += chunkSize) {
          await call(route, 'PUT', {position, content: bytes.subarray(position, position + chunkSize).toString('base64')});
        }
        await call(route, 'POST', {});
      } catch (error) {
        // A failed completion response may still mean the file was committed.
        try {
          const state = await (await call(route)).json();
          if (state.committed) return;
          await call(route, 'DELETE');
        } catch { /* X-Center ID expires incomplete uploads. */ }
        throw error;
      }
    },
    async remove(id) { await call('/' + encodeURIComponent(id), 'DELETE'); },
  };
}
