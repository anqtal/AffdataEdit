"""Publish immutable, hash-addressed files, then switch latest.json.

Usage: publish.py <prefix> <root> <required path>
  publish.py windows Build/Windows AffdataEdit.exe
  publish.py macos Build/macOS AffdataEdit.app/Contents/MacOS/AffdataEdit
  publish.py updater/windows <dir> AffdataEdit-Updater.exe
"""
import hashlib
import json
import os
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from urllib.parse import urlparse

import boto3
from botocore.config import Config
from botocore.exceptions import ClientError


def main():
    prefix, root, required = sys.argv[1].strip("/"), Path(sys.argv[2]), sys.argv[3]
    base = os.environ["R2_PUBLIC_URL"].rstrip("/")
    if urlparse(base).scheme != "https":
        raise ValueError("R2_PUBLIC_URL must be an HTTPS public bucket URL")
    bucket = os.environ["R2_BUCKET"]
    client = boto3.client(
        "s3", endpoint_url=f'https://{os.environ["R2_ACCOUNT_ID"]}.r2.cloudflarestorage.com',
        region_name="auto",
        config=Config(max_pool_connections=16),
    )
    entries = []
    seen = set()
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root).as_posix()
        # Burst writes debug information beside the player that is not shipped.
        if "_DoNotShip" in relative or not path.is_file():
            continue
        if path.is_symlink() or relative.lower() in seen:
            raise ValueError(f"Unsupported or duplicate path: {relative}")
        seen.add(relative.lower())
        with path.open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        entry = {"path": relative, "sha256": digest, "size": path.stat().st_size}
        # macOS programs and libraries keep their execute permission.
        if prefix.endswith("macos") and path.stat().st_mode & 0o111:
            entry["executable"] = True
        entries.append(entry)

    def ensure_object(entry):
        key = f"{prefix}/objects/{entry['sha256']}"
        try:
            existing = client.head_object(Bucket=bucket, Key=key)
            if existing["ContentLength"] != entry["size"]:
                raise ValueError(f"Unexpected object size: {key}")
        except ClientError as error:
            if error.response["Error"]["Code"] not in ("404", "NoSuchKey", "NotFound"):
                raise
            client.upload_file(str(root / entry["path"]), bucket, key, ExtraArgs={
                "CacheControl": "public, max-age=31536000, immutable",
                "ContentType": "application/octet-stream",
            })

    # Most objects already exist, so the time is network round trips: check and upload
    # concurrently. Any failure propagates before the manifest is written.
    with ThreadPoolExecutor(max_workers=16) as executor:
        for _ in executor.map(ensure_object, entries):
            pass
    if required.lower() not in seen:
        raise ValueError(f"{required} missing from {root}")
    manifest = json.dumps({
        "schema": 1, "version": os.environ["GITHUB_SHA"], "files": entries,
    }, ensure_ascii=False, indent=2).encode("utf-8")
    # Publish the pointer last: clients always see one complete build.
    for key in (f'{prefix}/releases/{os.environ["GITHUB_SHA"]}.json', f"{prefix}/latest.json"):
        client.put_object(Bucket=bucket, Key=key, Body=manifest,
                          ContentType="application/json", CacheControl="no-store")
    print(f"Published {len(entries)} files: {base}/{prefix}/latest.json")


if __name__ == "__main__":
    main()
