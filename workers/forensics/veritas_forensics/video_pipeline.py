from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image

from . import __version__
from .common import artifact, ensure_dir, finding, sha256_file, write_json
from .image_pipeline import perceptual_hashes

STRING_MARKERS = [
    "TikTok",
    "aweme",
    "musical.ly",
    "CapCut",
    "Jianying",
    "Dreamina",
    "Seedance",
    "ByteDance",
    "Douyin",
    "抖音",
    "剪映",
    "即梦",
]


def analyze_video(input_path: Path, out_dir: Path, json_path: Path, fps: float = 1.0) -> dict[str, Any]:
    ensure_dir(out_dir)
    artifacts: list[dict[str, str]] = []
    findings: list[dict[str, str]] = []
    payload: dict[str, Any] = {
        "tool": "veritas_forensics",
        "tool_version": __version__,
        "pipeline": "video",
        "input": str(input_path),
        "sha256": sha256_file(input_path),
        "file_size_bytes": input_path.stat().st_size,
        "artifacts": artifacts,
        "findings": findings,
    }

    payload["ffprobe"] = run_ffprobe(input_path)
    payload["codec_container"] = summarize_ffprobe(payload["ffprobe"])
    payload["string_scan"] = scan_strings(input_path)
    payload["ocr"] = {"tesseract_available": shutil.which("tesseract") is not None, "note": "OCR is not required for MVP; corner crops are OCR-ready artifacts when frames can be extracted."}

    frame_dir = out_dir / "frames"
    keyframe_dir = out_dir / "keyframes"
    corner_dir = out_dir / "corner_crops"
    extracted = extract_frames(input_path, frame_dir, fps=fps, max_frames=10)
    keyframes = extract_keyframes(input_path, keyframe_dir, max_frames=10)
    payload["frame_extraction"] = extracted
    payload["keyframe_extraction"] = keyframes

    frame_paths = sorted(frame_dir.glob("*.jpg"))
    keyframe_paths = sorted(keyframe_dir.glob("*.jpg"))
    for path in frame_paths:
        artifacts.append(artifact(path, out_dir, "frame"))
    for path in keyframe_paths:
        artifacts.append(artifact(path, out_dir, "keyframe"))

    corner_outputs = crop_corners(frame_paths[:5], corner_dir)
    for path in corner_outputs:
        artifacts.append(artifact(path, out_dir, "ocr-ready-corner-crop"))

    payload["keyframe_hashes"] = [{"frame": path.name, "hashes": safe_hash_image(path)} for path in keyframe_paths[:10]]
    payload["frame_difference_summary"] = frame_difference_summary(frame_paths)
    payload["optional_optical_flow"] = optional_optical_flow(frame_paths[:5])

    markers = payload["string_scan"]["matches"]
    if markers:
        findings.append(finding(
            category="VideoTemporal",
            claim="Possible app or tool trace strings were found in the video bytes.",
            confidence="Medium",
            direction="Neutral",
            evidence=", ".join(sorted(markers)),
            limitations="Embedded strings can come from platforms, editors, repost chains, metadata, or unrelated byte sequences. This does not prove AI generation.",
            falsification_path="Compare against the platform original and inspect metadata, watermarks, and source chronology.",
        ))
    else:
        findings.append(finding(
            category="VideoTemporal",
            claim="No configured app or tool trace strings were found in the video bytes.",
            confidence="Medium",
            direction="Neutral",
            evidence="The string scan did not match TikTok, CapCut, ByteDance, Douyin, Dreamina, Seedance, or configured Chinese strings.",
            limitations="Absence of embedded strings does not prove the video is original or authentic.",
            falsification_path="Provide original source media and compare with platform metadata and visible watermarks.",
        ))

    if payload["ffprobe"]["available"]:
        findings.append(finding(
            category="Metadata",
            claim="ffprobe metadata was extracted for container and stream review.",
            confidence="High",
            direction="Neutral",
            evidence=f"Container: {payload['codec_container'].get('format_name', 'unknown')}; video codec: {payload['codec_container'].get('video_codec', 'unknown')}.",
            limitations="Video metadata is often rewritten by platforms and editors and is not sufficient for attribution.",
            falsification_path="Compare metadata against original exports and earlier platform copies.",
        ))
    else:
        findings.append(finding(
            category="Metadata",
            claim="ffprobe metadata was not available.",
            confidence="High",
            direction="Inconclusive",
            evidence=payload["ffprobe"].get("error", "ffprobe unavailable"),
            limitations="Missing tool output limits automated video review but does not itself support authenticity or manipulation claims.",
            falsification_path="Install ffprobe or provide an ffprobe JSON export from a trusted local environment.",
        ))

    findings.append(finding(
        category="VideoTemporal",
        claim="Temporal artifact inspection remains required.",
        confidence="Low",
        direction="Inconclusive",
        evidence="Representative frames, keyframes, and corner crops were generated when ffmpeg was available.",
        limitations="Single-frame or metadata heuristics must not be used to conclude that a video is AI-generated.",
        falsification_path="Review first/middle/last frames, audio/source matches, watermarks, and original upload chronology.",
    ))

    payload["summary"] = "Video analysis completed with conservative metadata, string-scan, frame, and crop outputs where local tools were available."
    write_json(json_path, payload)
    return payload


def run_ffprobe(path: Path) -> dict[str, Any]:
    if shutil.which("ffprobe") is None:
        return {"available": False, "error": "ffprobe not installed"}
    result = subprocess.run(
        ["ffprobe", "-v", "error", "-print_format", "json", "-show_format", "-show_streams", str(path)],
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        return {"available": False, "error": result.stderr.strip() or "ffprobe failed"}
    try:
        parsed = json.loads(result.stdout)
    except json.JSONDecodeError as exc:
        return {"available": False, "error": f"ffprobe JSON parse failed: {exc}"}
    parsed["available"] = True
    return parsed


def summarize_ffprobe(ffprobe: dict[str, Any]) -> dict[str, Any]:
    if not ffprobe.get("available"):
        return {}
    streams = ffprobe.get("streams", [])
    video = next((s for s in streams if s.get("codec_type") == "video"), {})
    audio = next((s for s in streams if s.get("codec_type") == "audio"), {})
    fmt = ffprobe.get("format", {})
    return {
        "format_name": fmt.get("format_name"),
        "duration_seconds": float(fmt["duration"]) if fmt.get("duration") else None,
        "video_codec": video.get("codec_name"),
        "audio_codec": audio.get("codec_name"),
        "width": video.get("width"),
        "height": video.get("height"),
        "frame_rate": video.get("avg_frame_rate") or video.get("r_frame_rate"),
        "metadata_tags": fmt.get("tags", {}),
    }


def scan_strings(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    matches: dict[str, int] = {}
    for marker in STRING_MARKERS:
        count = data.count(marker.encode("utf-8"))
        if count == 0:
            count = data.lower().count(marker.lower().encode("utf-8"))
        if count:
            matches[marker] = count
    return {"markers": STRING_MARKERS, "matches": matches}


def extract_frames(path: Path, out_dir: Path, fps: float, max_frames: int) -> dict[str, Any]:
    ensure_dir(out_dir)
    if shutil.which("ffmpeg") is None:
        return {"available": False, "error": "ffmpeg not installed", "files": []}
    pattern = out_dir / "frame_%04d.jpg"
    result = subprocess.run(
        ["ffmpeg", "-y", "-i", str(path), "-vf", f"fps={fps}", "-frames:v", str(max_frames), str(pattern)],
        capture_output=True,
        text=True,
        check=False,
    )
    files = sorted(p.name for p in out_dir.glob("*.jpg"))
    return {"available": result.returncode == 0, "error": None if result.returncode == 0 else result.stderr[-1000:], "files": files}


def extract_keyframes(path: Path, out_dir: Path, max_frames: int) -> dict[str, Any]:
    ensure_dir(out_dir)
    if shutil.which("ffmpeg") is None:
        return {"available": False, "error": "ffmpeg not installed", "files": []}
    pattern = out_dir / "keyframe_%04d.jpg"
    result = subprocess.run(
        ["ffmpeg", "-y", "-skip_frame", "nokey", "-i", str(path), "-frames:v", str(max_frames), "-vsync", "0", str(pattern)],
        capture_output=True,
        text=True,
        check=False,
    )
    files = sorted(p.name for p in out_dir.glob("*.jpg"))
    return {"available": result.returncode == 0, "error": None if result.returncode == 0 else result.stderr[-1000:], "files": files}


def crop_corners(frame_paths: list[Path], out_dir: Path) -> list[Path]:
    ensure_dir(out_dir)
    outputs: list[Path] = []
    for frame in frame_paths:
        with Image.open(frame) as image:
            w, h = image.size
            crop_w = max(1, int(w * 0.25))
            crop_h = max(1, int(h * 0.20))
            boxes = {
                "top_left": (0, 0, crop_w, crop_h),
                "top_right": (w - crop_w, 0, w, crop_h),
                "bottom_left": (0, h - crop_h, crop_w, h),
                "bottom_right": (w - crop_w, h - crop_h, w, h),
            }
            for name, box in boxes.items():
                output = out_dir / f"{frame.stem}_{name}.jpg"
                image.crop(box).save(output, "JPEG", quality=92)
                outputs.append(output)
    return outputs


def safe_hash_image(path: Path) -> dict[str, str | None]:
    try:
        with Image.open(path) as image:
            return perceptual_hashes(image)
    except Exception:
        return {"aHash_bits": None, "dHash_bits": None, "pHash": None}


def frame_difference_summary(frame_paths: list[Path]) -> dict[str, Any]:
    if len(frame_paths) < 2:
        return {"available": False, "reason": "fewer than two frames extracted"}
    diffs: list[float] = []
    previous: np.ndarray | None = None
    for path in frame_paths:
        with Image.open(path) as image:
            arr = np.asarray(image.convert("L").resize((160, 90)), dtype=np.float32)
        if previous is not None:
            diffs.append(float(np.mean(np.abs(arr - previous))))
        previous = arr
    return {"available": True, "mean_abs_diff": float(np.mean(diffs)), "max_abs_diff": float(np.max(diffs)), "sample_count": len(diffs)}


def optional_optical_flow(frame_paths: list[Path]) -> dict[str, Any]:
    try:
        import cv2
    except Exception:
        return {"available": False, "reason": "opencv-python is not installed"}
    if len(frame_paths) < 2:
        return {"available": False, "reason": "fewer than two frames extracted"}
    flows: list[float] = []
    previous = None
    for path in frame_paths:
        with Image.open(path) as image:
            arr = np.asarray(image.convert("L").resize((160, 90)))
        if previous is not None:
            flow = cv2.calcOpticalFlowFarneback(previous, arr, None, 0.5, 3, 15, 3, 5, 1.2, 0)
            flows.append(float(np.mean(np.linalg.norm(flow, axis=2))))
        previous = arr
    return {"available": True, "mean_flow_magnitude": float(np.mean(flows)), "sample_count": len(flows)}
