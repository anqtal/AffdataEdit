"""Publish immutable, hash-addressed Windows files, then switch latest.json."""
import hashlib
import json
import os
from pathlib import Path
from urllib.parse import urlparse

import boto3
from botocore.exceptions import ClientError


def main():
    root = Path("Build/Windows")
    base = os.environ["R2_PUBLIC_URL"].rstrip("/")
    if urlparse(base).scheme != "https":
        raise ValueError("R2_PUBLIC_URL must be an HTTPS public bucket URL")
    bucket = os.environ["R2_BUCKET"]
    client = boto3.client(
        "s3", endpoint_url=f'https://{os.environ["R2_ACCOUNT_ID"]}.r2.cloudflarestorage.com',
        region_name="auto",
    )
    entries = []
    seen = set()
    for path in sorted(root.rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(root).as_posix()
        if path.is_symlink() or relative.lower() in seen:
            raise ValueError(f"Unsupported or duplicate Windows path: {relative}")
        seen.add(relative.lower())
        with path.open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        key = f"windows/objects/{digest}"
        try:
            existing = client.head_object(Bucket=bucket, Key=key)
            if existing["ContentLength"] != path.stat().st_size:
                raise ValueError(f"Unexpected object size: {key}")
        except ClientError as error:
            if error.response["Error"]["Code"] not in ("404", "NoSuchKey", "NotFound"):
                raise
            client.upload_file(str(path), bucket, key, ExtraArgs={
                "CacheControl": "public, max-age=31536000, immutable",
                "ContentType": "application/octet-stream",
            })
        entries.append({"path": relative, "sha256": digest, "size": path.stat().st_size})
    if "affdataedit.exe" not in seen or "affdataedit-updater.exe" not in seen:
        raise ValueError("Player or updater missing from build")
    manifest = json.dumps({
        "schema": 1, "version": os.environ["GITHUB_SHA"], "files": entries,
    }, ensure_ascii=False, indent=2).encode("utf-8")
    # Publish the pointer last: clients always see one complete build.
    for key in (f'windows/releases/{os.environ["GITHUB_SHA"]}.json', "windows/latest.json"):
        client.put_object(Bucket=bucket, Key=key, Body=manifest,
                          ContentType="application/json", CacheControl="no-store")
    print(f"Published {len(entries)} files: {base}/windows/latest.json")


if __name__ == "__main__":
    main()
