// SPDX-License-Identifier: AGPL-3.0-only
import { createHash, randomBytes, timingSafeEqual } from 'node:crypto';
import { createServer } from 'node:http';
import { existsSync, mkdirSync, readFileSync, readdirSync, unlinkSync, writeFileSync } from 'node:fs';
import { fileLimit, appFiles } from './app-files.mjs';
import { writeSharedRecords } from './shared-records.mjs';
import { personalFiles } from './personal-files.mjs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import * as oidc from 'openid-client';

const issuer = new URL('https://id.xcenter.it');
const token = () => randomBytes(32).toString('base64url');
const digest = (value) => createHash('sha256').update(value).digest('hex');
const transactionLifetime = 5 * 60_000;
const sessionLifetime = 8 * 60 * 60_000;
const persistentLifetime = 14 * 24 * 60 * 60_000;
const snapshotPattern = /^(\d{1,5}\.\d{1,5}\.\d{1,5})(?:-(?:manual|auto)-\d{17}-[a-f0-9]{8})?$/;
const maxBackup = fileLimit;

async function body(request, limit) {
  const pieces = [];
  let count = 0;
  for await (const piece of request) {
    count += piece.length;
    if (count > limit) throw Object.assign(new Error('Request too large'), { status: 413 });
    pieces.push(piece);
  }
  return Buffer.concat(pieces);
}

function reply(response, status, value, headers = {}) {
  response.writeHead(status, { 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff',
    'Referrer-Policy': 'no-referrer', 'Content-Type': 'application/json', ...headers });
  response.end(value === undefined ? undefined : Buffer.isBuffer(value) ? value : JSON.stringify(value));
}

export function service({ configuration, origin, storage, exchange = oidc.authorizationCodeGrant, refresh = oidc.refreshTokenGrant, files = appFiles, legacyFiles = personalFiles }) {
  const callback = new URL('/api/account/callback', origin);
  const pending = new Map();
  const sessions = new Map();
  const locks = new Map();

  return createServer(async (request, response) => {
    try {
      const now = Date.now();
      for (const records of [pending, sessions]) {
        for (const [key, value] of records) if (value.until <= now) records.delete(key);
      }
      const url = new URL(request.url, origin);
      const method = request.method;
      if (method === 'GET' && url.pathname === '/healthz') return reply(response, 200, { status: 'ok' });

      if (method === 'POST' && url.pathname === '/api/account/start') {
        const { challenge } = JSON.parse(await body(request, 1024));
        if (typeof challenge !== 'string' || !/^[a-f0-9]{64}$/.test(challenge))
          return reply(response, 400, { error: 'Invalid pickup challenge' });
        if (pending.size >= 256) return reply(response, 503, { error: 'Sign-in is busy' });
        const handle = token();
        const state = token();
        const nonce = token();
        const verifier = oidc.randomPKCECodeVerifier();
        const codeChallenge = await oidc.calculatePKCECodeChallenge(verifier);
        pending.set(handle, { state, nonce, verifier, challenge, callbackUsed: false, until: now + transactionLifetime });
        const authorization = oidc.buildAuthorizationUrl(configuration, {
          redirect_uri: callback.href, scope: 'openid offline_access app_data', prompt: 'consent', state, nonce,
          code_challenge: codeChallenge, code_challenge_method: 'S256',
        });
        return reply(response, 201, { handle, authorization: authorization.href });
      }

      if (method === 'GET' && url.pathname === callback.pathname) {
        const state = url.searchParams.get('state');
        const entry = [...pending.values()].find(item => item.state === state);
        if (!entry || entry.callbackUsed) return reply(response, 400, { error: 'Invalid or used sign-in' });
        entry.callbackUsed = true;
        const callbackUrl = new URL(callback);
        callbackUrl.search = url.search;
        let result;
        try {
          result = await exchange(configuration, callbackUrl, {
            expectedState: entry.state, expectedNonce: entry.nonce, pkceCodeVerifier: entry.verifier,
            idTokenExpected: true,
          });
        } catch (error) {
          entry.failure = error.error === 'invalid_client' ? 'client_rejected' : 'sign_in_rejected';
          console.error('Player Sync callback rejected:', entry.failure);
          return reply(response, 502, Buffer.from(entry.failure === 'client_rejected'
            ? 'X-Center ID rejected the server credentials. Return to Beat Saber.'
            : 'X-Center ID could not complete sign-in. Return to Beat Saber.'), {
            'Content-Type': 'text/plain; charset=utf-8',
          });
        }
        const claims = result.claims();
        if (!claims || claims.iss !== issuer.href.replace(/\/$/, '') || typeof claims.sub !== 'string')
          return reply(response, 401, { error: 'Invalid account' });
        entry.access = result.access_token;
        entry.refresh = typeof result.refresh_token === 'string' ? result.refresh_token : null;
        entry.refreshAfter = now + Math.max(0, (Number(result.expires_in) || 60) - 60) * 1000;
        entry.owner = digest(JSON.stringify([claims.iss, claims.sub]));
        return reply(response, 200, Buffer.from('Sign-in complete. Return to Beat Saber.'), {
          'Content-Type': 'text/plain; charset=utf-8',
        });
      }

      if (method === 'POST' && url.pathname === '/api/account/pickup') {
        const { handle, proof } = JSON.parse(await body(request, 1024));
        const entry = pending.get(handle);
        if (!entry || typeof proof !== 'string' || !timingSafeEqual(Buffer.from(digest(proof)), Buffer.from(entry.challenge)))
          return reply(response, 401, { error: 'Invalid or expired pickup' });
        if (entry.failure) {
          pending.delete(handle);
          return reply(response, 403, { error: entry.failure });
        }
        if (!entry.owner) return reply(response, 202, { status: 'pending' });
        if (sessions.size >= 1024) return reply(response, 503, { error: 'Sign-in is busy' });
        pending.delete(handle);
        const session = token();
        const lifetime = entry.refresh ? persistentLifetime : sessionLifetime;
        sessions.set(digest(session), { owner: entry.owner, until: now + lifetime,
          access: entry.access, refresh: entry.refresh, refreshAfter: entry.refreshAfter });
        return reply(response, 200, { session, expiresIn: lifetime / 1000, persistent: Boolean(entry.refresh) });
      }

      const bearer = request.headers.authorization?.match(/^Bearer ([A-Za-z0-9_-]{43})$/)?.[1];
      const sessionKey = bearer && digest(bearer);
      const session = sessions.get(sessionKey);
      if (!session) return reply(response, 401, { error: 'Sign in required' });
      if (method === 'POST' && url.pathname === '/api/account/logout') {
        sessions.delete(sessionKey);
        return reply(response, 204);
      }
      if (session.refresh && session.refreshAfter <= now) {
        const previous = session.refresh;
        session.renewing ??= Promise.resolve().then(() => refresh(configuration, previous))
          .then((tokens) => {
            if (!tokens.refresh_token) throw new Error('Missing rotated credential');
            session.access = tokens.access_token;
            session.refresh = tokens.refresh_token;
            session.refreshAfter = Date.now() + Math.max(1, (Number(tokens.expires_in) || 60) - 60) * 1000;
          }).finally(() => { session.renewing = null; });
        try { await session.renewing; }
        catch {
          sessions.delete(sessionKey);
          return reply(response, 401, { error: 'Sign in again' });
        }
        if (!sessions.has(sessionKey)) return reply(response, 401, { error: 'Sign in again' });
      }
      if (method === 'POST' && url.pathname === '/api/exports') {
        const input = JSON.parse(await body(request, 128 * 1024));
        if (!sessions.has(sessionKey) || session.until <= Date.now())
          return reply(response, 401, { error: 'Session expired' });
        await writeSharedRecords(input, session.access, configuration.clientMetadata().client_id);
        return reply(response, 204);
      }
      const listing = method === 'GET' && url.pathname === '/api/backups';
      const version = url.pathname.match(/^\/api\/backups\/([^/]+)$/)?.[1];
      if (!listing && (!version || !snapshotPattern.test(version)))
        return reply(response, 404, { error: 'Unknown route' });
      if (!['GET', 'HEAD', 'PUT', 'DELETE'].includes(method))
        return reply(response, 405, { error: 'Unsupported method' });
      const uploaded = method === 'PUT' ? await body(request, maxBackup) : undefined;
      if (uploaded && (request.headers['content-type'] !== 'application/zip' ||
          uploaded.length < 4 || uploaded.readUInt32LE(0) !== 0x04034b50))
        return reply(response, 400, { error: 'Expected a backup ZIP' });
      if (locks.has(session.owner)) return reply(response, 409, { error: 'Backup operation already running' });
      if (!sessions.has(sessionKey) || session.until <= Date.now())
        return reply(response, 401, { error: 'Session expired' });
      locks.set(session.owner, true);
      try {
        const personal = files(session.access, configuration.clientMetadata().client_id);
        let entries = await personal.list();
        const legacy = resolve(storage, session.owner);
        if (listing && existsSync(legacy)) {
          for (const name of readdirSync(legacy)) {
            if (!name.endsWith('.zip') || !snapshotPattern.test(name.slice(0, -4))) continue;
            const path = resolve(legacy, name);
            const bytes = readFileSync(path);
            if (bytes.length > maxBackup) continue;
            const existing = entries.find(item => item.name === name);
            if (!existing) await personal.save(name, bytes);
            entries = await personal.list();
            const migrated = entries.filter(item => item.name === name);
            if (migrated.length === 1 && digest(await personal.read(migrated[0].id)) === digest(bytes))
              unlinkSync(path);
          }
        }
        if (listing && !session.checkedLegacyFiles) {
          try {
            const previous = legacyFiles(session.access);
            for (const item of await previous.list()) {
              const imported = resolve(legacy, 'personal-imports', digest(item.id));
              if (existsSync(imported)) continue;
              if (!item.name.endsWith('.zip') || !snapshotPattern.test(item.name.slice(0, -4)) ||
                  item.size > 2 * 1024 * 1024) continue;
              const bytes = await previous.read(item.id);
              if (bytes.length < 4 || bytes.readUInt32LE(0) !== 0x04034b50) continue;
              if (!entries.some(entry => entry.name === item.name)) {
                await personal.save(item.name, bytes);
                entries = await personal.list();
              }
              const matches = entries.filter(entry => entry.name === item.name);
              if (matches.length === 1 && digest(await personal.read(matches[0].id)) === digest(bytes)) {
                mkdirSync(resolve(legacy, 'personal-imports'), {recursive: true, mode: 0o700});
                writeFileSync(imported, '', {mode: 0o600, flag: 'wx'});
              }
            }
            session.checkedLegacyFiles = true;
          } catch (error) {
            if (error.status !== 403) console.error('Legacy Files import deferred');
          }
        }
        if (listing) return reply(response, 200, {
          versions: entries.filter(item => item.name.endsWith('.zip') && snapshotPattern.test(item.name.slice(0, -4)))
            .map(item => ({ version: item.name.slice(0, -4), modified: new Date(item.created).toISOString() })),
        });
        const name = version + '.zip';
        const matches = entries.filter(item => item.name === name);
        if (matches.length > 1) return reply(response, 409, { error: 'Duplicate backup in Files' });
        const entry = matches[0];
        const saved = entry && await personal.read(entry.id);
        const revision = saved ? '"' + digest(saved) + '"' : '"0"';
        if (method === 'GET' || method === 'HEAD') {
          return saved ? reply(response, 200, method === 'HEAD' ? undefined : saved,
            { 'Content-Type': 'application/zip', ETag: revision })
            : reply(response, 404, { error: 'No backup for this version' });
        }
        if (request.headers['if-match'] !== revision) return reply(response, 412, { error: 'Backup changed' });
        if (method === 'DELETE') {
          if (entry) await personal.remove(entry.id);
          return reply(response, 204);
        }
        if (entry) return reply(response, 409, { error: 'Delete the existing snapshot before replacing it' });
        await personal.save(name, uploaded);
        return reply(response, 204, undefined, { ETag: '"' + digest(uploaded) + '"' });
      } finally { locks.delete(session.owner); }
    } catch (error) {
      return reply(response, error.status || (error instanceof SyntaxError ? 400 : 502),
        { error: 'Request could not be completed' });
    }
  });
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const { XID_CLIENT_ID, XID_CLIENT_SECRET, BACKUP_STORAGE, PORT = '8080' } = process.env;
  if (!XID_CLIENT_ID || !XID_CLIENT_SECRET || !BACKUP_STORAGE) throw new Error('Set the server environment first');
  const configuration = await oidc.discovery(issuer, XID_CLIENT_ID, {}, oidc.ClientSecretBasic(XID_CLIENT_SECRET));
  oidc.enableNonRepudiationChecks(configuration);
  service({ configuration, origin: 'https://beatsaber.xcenter.it', storage: BACKUP_STORAGE })
    .listen(Number(PORT), '0.0.0.0');
}
