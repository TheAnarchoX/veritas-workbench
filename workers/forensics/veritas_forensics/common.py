from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def write_json(path: Path, payload: dict[str, Any]) -> None:
    ensure_dir(path.parent)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")


def artifact(path: Path, root: Path, kind: str) -> dict[str, str]:
    return {
        "filename": path.name,
        "path": str(path.relative_to(root).as_posix()),
        "type": kind,
    }


def finding(
    *,
    category: str,
    claim: str,
    confidence: str,
    direction: str,
    evidence: str,
    limitations: str,
    falsification_path: str,
) -> dict[str, str]:
    return {
        "category": category,
        "claim": claim,
        "confidence": confidence,
        "direction": direction,
        "evidence": evidence,
        "limitations": limitations,
        "falsification_path": falsification_path,
    }
