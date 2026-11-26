# SPDX-License-Identifier: AGPL-3.0-only
"""Synchronize one versioned backup using the X-Center ID browser handoff."""
import argparse
from hashlib import sha256
import json
import os
from pathlib import Path
import re
import secrets
import tempfile
import time
from urllib.error import HTTPError
from urllib.parse import urlsplit
from urllib.request import HTTPRedirectHandler, Request, build_opener
import webbrowser
import zipfile

ORIGIN = "https://beatsaber.xcenter.it"
MAX_ZIP = 32 * 1024**2


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def call(path, method="GET", *, data=None, session=None, revision=None):
    headers = {}
    if data is not None:
        if isinstance(data, bytes):
            headers["Content-Type"] = "application/zip"
        else:
            data = json.dumps(data).encode()
            headers["Content-Type"] = "application/json"
    if session:
        headers["Authorization"] = "Bearer " + session
    if revision:
        headers["If-Match"] = revision
    try:
        response = build_opener(NoRedirect()).open(
            Request(ORIGIN + path, data=data, method=method, headers=headers), timeout=30)
    except HTTPError as error:
        response = error
    with response:
        body = response.read(MAX_ZIP + 1)
        if len(body) > MAX_ZIP:
            raise ValueError("Response exceeds backup size limit")
        return response.status, response.headers, body


def authorize():
    proof = secrets.token_urlsafe(32)
    status, _, body = call("/api/account/start", "POST", data={"challenge": sha256(proof.encode()).hexdigest()})
    if status != 201:
        raise ValueError(f"Sign-in service returned HTTP {status}")
    transaction = json.loads(body)
    authorization = urlsplit(transaction["authorization"])
    if (authorization.scheme, authorization.netloc, authorization.path) != ("https", "id.xcenter.it", "/auth"):
        raise ValueError("Unexpected authorization origin")
    webbrowser.open(transaction["authorization"])
    for _ in range(95):
        status, _, body = call("/api/account/pickup", "POST",
                               data={"handle": transaction["handle"], "proof": proof})
        if status == 200:
            return json.loads(body)["session"]
        if status != 202:
            raise ValueError(f"Sign-in pickup returned HTTP {status}")
        time.sleep(3)
    raise ValueError("Sign-in timed out")


def validate_zip(path, version):
    if path.stat().st_size > MAX_ZIP:
        raise ValueError("Backup exceeds the cloud size limit")
    with zipfile.ZipFile(path) as archive:
        manifest = archive.getinfo("manifest.json")
        if manifest.file_size > 16 * 1024**2:
            raise ValueError("Backup manifest is too large")
        data = json.loads(archive.read(manifest))
        if data["schema"] != 1 or data["gameVersion"] != version:
            raise ValueError("Backup is for a different game version")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["push", "pull", "delete"])
    parser.add_argument("backup", type=Path, help="Backup file named GAME_VERSION.zip")
    parser.add_argument("--replace", action="store_true", help="Allow replacing the destination backup")
    args = parser.parse_args()
    version = args.backup.stem
    if args.backup.suffix != ".zip" or not re.fullmatch(r"[0-9]{1,5}\.[0-9]{1,5}\.[0-9]{1,5}", version):
        parser.error("expected GAME_VERSION.zip")
    if args.action == "push":
        validate_zip(args.backup, version)
    if args.action == "pull" and args.backup.exists() and not args.replace:
        parser.error("local backup exists, use --replace to overwrite it")
    if args.action == "delete" and not args.replace:
        parser.error("use --replace to confirm deletion")
    session = authorize()
    url = "/api/backups/" + version
    try:
        status, headers, data = call(url, session=session)
        if args.action == "pull":
            if status != 200:
                raise ValueError(f"Download returned HTTP {status}")
            args.backup.parent.mkdir(parents=True, mode=0o700, exist_ok=True)
            with tempfile.TemporaryDirectory(dir=args.backup.parent) as folder:
                staged = Path(folder) / args.backup.name
                staged.write_bytes(data)
                validate_zip(staged, version)
                staged.chmod(0o600)
                os.replace(staged, args.backup)
        else:
            if status not in (200, 404):
                raise ValueError(f"Backup lookup returned HTTP {status}")
            if status == 200 and not args.replace:
                raise ValueError("Cloud backup exists. Use --replace to overwrite it.")
            updating = args.action == "push"
            status, _, _ = call(url, "PUT" if updating else "DELETE",
                                data=args.backup.read_bytes() if updating else None,
                                session=session, revision=headers["ETag"] if status == 200 else '"0"')
            if status != 204:
                raise ValueError(f"Backup update returned HTTP {status}")
    finally:
        try:
            call("/api/account/logout", "POST", session=session)
        except OSError:
            pass


if __name__ == "__main__":
    main()
