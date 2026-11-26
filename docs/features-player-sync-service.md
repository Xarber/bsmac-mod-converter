# Player Sync service

The Node 22 service lives at `implementation/services/player-sync`. It owns the registered callback `https://beatsaber.xcenter.it/api/account/callback` and the browser handoff. The installed menu queues local operations. The accompanying CLI handles explicit cloud transfers after the service is live.

## Deploy on X-Center

Run a single instance. Use the included Dockerfile or run `npm ci` followed by `npm start` with Node 22. Route the HTTPS host `beatsaber.xcenter.it` to port 8080 and check `GET /healthz`. Point `BACKUP_STORAGE` at a private persistent volume that the service user owns. Set `XID_CLIENT_ID` and `XID_CLIENT_SECRET` through the server's secret configuration and verify the exact callback and active registration in the developer portal. Serve neither the data directory nor environment files over HTTP. Maintain TLS and volume encryption and keep reverse proxy logs free of callback query strings and authorization headers. Run service dependency and request tests before deployment.

The service uses the X-Center ID authorization code flow with PKCE, nonce, state, signature validation, and a one-time, proof-bound browser handoff. It requests delegated `app_data` consent and stores browseable ZIPs in the user's branded Beat Saber app folder, charged against the user's quota. Normal sign-in requests no Files read permission. The legacy import remains available to an existing session that already has the scope. The migration leaves those originals in place and records which verified files were imported. There are no new service-storage ZIP writes. Transfers through Player Sync are capped at 32 MiB per ZIP. Sign-outs revoke the current service session. Restarting the service clears sessions and pending handoffs.

## Cloud operations

From the installed worker environment, run `python -m bsmac_converter.backup_sync push /path/to/1.40.8.zip` to upload, `pull` to download, or `delete --replace` to remove the cloud copy. Use `--replace` to confirm overwriting an existing local or remote ZIP. The browser opens X-Center ID and the CLI waits for account authorization. Restore remains an explicit local step with category choices.

## Verification

The local HTTP test mocks the issuer exchange and verifies proof binding, single-use callback and pickup, per-account isolation, revision conflicts, and logout. Live issuer consent, the public callback, and actual cloud transfer require the deployed service.

## Deployment record

On November 26, 2026 X-Center deployed backend revision `82befe4` with the original registration and shared persistent storage. The loopback service is on port 18423. Public health, unauthenticated backup rejection, issuer sign-in page, and proof-bound pending pickup were verified. A user-signed-in callback and live backup transfer remain pending. The appliance change record contains rollback steps.

## Editable backup names

Backup uploads set both Browseable and Editable on the user's app-container files. Existing tracked Browseable backups are upgraded when listed. Backups use the X-Center file ID and the original SHA-256 in a per-account index on the persistent volume. Legacy filenames seed this index when they identify a single file with a valid digest. Dashboard renames preserve snapshot identity. Replaced bytes are rejected as a conflict, and deleting a cloud file leaves any local copy available. Downloaded bytes are checked against the reported digest and the backup manifest. Player Backup displays a cloud label only if any local ZIP under that snapshot ID has identical bytes. The local ZIP retains its stable filename for the restore worker.

BeatLeader replay files are currently local. The backup upload route handles snapshot ZIPs only, so it does not grant Editable to standalone replay files.

**Sync cloud backups** refreshes the cloud list and upgrades existing tracked Browseable files. It checks local snapshots against the listed SHA-256 hashes, uploads missing snapshots, and reports conflicts without overwriting cloud or local files. On November 26, 2026 the backend was deployed at `7e7b06c`. The container tests passed, public health responded and unauthenticated backup listing returned HTTP 401. A signed-in upload and dashboard rename have not been checked live.
