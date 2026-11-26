// SPDX-License-Identifier: AGPL-3.0-only
const folderName = 'Beat Saber Player Backups';
export const fileLimit = 2 * 1024 * 1024;

export function personalFiles(access, request = fetch) {
  async function call(route, method = 'GET', data) {
    if (!access) throw Object.assign(new Error('Files consent required'), { status: 403 });
    const response = await request('https://id.xcenter.it/v1/files' + route, {
      method, redirect: 'error', signal: AbortSignal.timeout(20000),
      headers: { Authorization: 'Bearer ' + access, ...(data ? { 'Content-Type': 'application/json' } : {}) },
      body: data && JSON.stringify(data),
    });
    if (!response.ok) {
      const status = response.status === 401 ? 403 : ([403, 404, 409, 413].includes(response.status) ? response.status : 502);
      throw Object.assign(new Error('Personal Files request failed'), { status });
    }
    return response;
  }

  async function folder(create = false) {
    const root = await (await call('')).json();
    const existing = root.folders.filter(item => item.name === folderName);
    if (existing.length > 1) throw Object.assign(new Error('Multiple backup folders'), { status: 409 });
    if (existing.length) return existing[0].id;
    return create ? (await (await call('/folders', 'POST', { name: folderName })).json()).id : null;
  }

  return {
    async list() {
      const id = await folder();
      return id ? (await (await call('?folder=' + encodeURIComponent(id))).json()).files : [];
    },
    async read(id) {
      const response = await call('/' + encodeURIComponent(id));
      return Buffer.from(await response.arrayBuffer());
    },
    async save(name, bytes) {
      if (bytes.length > fileLimit) throw Object.assign(new Error('Backup exceeds the Files upload limit'), { status: 413 });
      await call('', 'POST', { folder: await folder(true), name, content: bytes.toString('base64') });
    },
    async remove(id) { await call('/' + encodeURIComponent(id), 'DELETE'); },
  };
}
