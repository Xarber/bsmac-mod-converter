// SPDX-License-Identifier: AGPL-3.0-only
import { createHash, randomBytes, timingSafeEqual } from 'node:crypto';
import { createServer } from 'node:http';
import { existsSync, mkdirSync, readFileSync, renameSync, unlinkSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import * as oidc from 'openid-client';

const issuer = new URL('https://id.xcenter.it');
const token = () => randomBytes(32).toString('base64url');
const digest = (value) => createHash('sha256').update(value).digest('hex');
const transactionLifetime = 5 * 60_000;
const sessionLifetime = 8 * 60 * 60_000;
const maxBackup = 32 * 1024 * 1024;

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

export function service({ configuration, origin, storage, exchange = oidc.authorizationCodeGrant }) {
  const callback = new URL('/api/account/callback', origin);
  const pending = new Map();
  const sessions = new Map();

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
          redirect_uri: callback.href, scope: 'openid', state, nonce,
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
        const result = await exchange(configuration, callbackUrl, {
          expectedState: entry.state, expectedNonce: entry.nonce, pkceCodeVerifier: entry.verifier,
          idTokenExpected: true,
        });
        const claims = result.claims();
        if (!claims || claims.iss !== issuer.href.replace(/\/$/, '') || typeof claims.sub !== 'string')
          return reply(response, 401, { error: 'Invalid account' });
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
        if (!entry.owner) return reply(response, 202, { status: 'pending' });
        if (sessions.size >= 1024) return reply(response, 503, { error: 'Sign-in is busy' });
        pending.delete(handle);
        const session = token();
        sessions.set(digest(session), { owner: entry.owner, until: now + sessionLifetime });
        return reply(response, 200, { session, expiresIn: sessionLifetime / 1000 });
      }

      const bearer = request.headers.authorization?.match(/^Bearer ([A-Za-z0-9_-]{43})$/)?.[1];
      const sessionKey = bearer && digest(bearer);
      const session = sessions.get(sessionKey);
      if (!session) return reply(response, 401, { error: 'Sign in required' });
      if (method === 'POST' && url.pathname === '/api/account/logout') {
        sessions.delete(sessionKey);
        return reply(response, 204);
      }
      const version = url.pathname.match(/^\/api\/backups\/(\d{1,5}\.\d{1,5}\.\d{1,5})$/)?.[1];
      if (!version) return reply(response, 404, { error: 'Unknown route' });
      const folder = resolve(storage, session.owner);
      const target = resolve(folder, version + '.zip');
      const current = () => existsSync(target) ? readFileSync(target) : undefined;
      if (method === 'GET') {
        const saved = current();
        return saved ? reply(response, 200, saved, { 'Content-Type': 'application/zip', ETag: '"' + digest(saved) + '"' })
          : reply(response, 404, { error: 'No backup for this version' });
      }
      if (method !== 'PUT' && method !== 'DELETE') return reply(response, 405, { error: 'Unsupported method' });
      const uploaded = method === 'PUT' ? await body(request, maxBackup) : undefined;
      if (!sessions.has(sessionKey) || session.until <= Date.now()) return reply(response, 401, { error: 'Session expired' });
      const saved = current();
      const previous = saved ? '"' + digest(saved) + '"' : '"0"';
      if (request.headers['if-match'] !== previous) return reply(response, 412, { error: 'Backup changed', revision: previous });
      if (method === 'DELETE') {
        if (saved) unlinkSync(target);
        return reply(response, 204);
      }
      if (request.headers['content-type'] !== 'application/zip' ||
          uploaded.length < 4 || uploaded.readUInt32LE(0) !== 0x04034b50)
        return reply(response, 400, { error: 'Expected a backup ZIP' });
      mkdirSync(folder, { recursive: true, mode: 0o700 });
      const temporary = target + '.' + token();
      writeFileSync(temporary, uploaded, { mode: 0o600, flag: 'wx' });
      try { renameSync(temporary, target); }
      finally { if (existsSync(temporary)) unlinkSync(temporary); }
      return reply(response, 204, undefined, { ETag: '"' + digest(uploaded) + '"' });
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
