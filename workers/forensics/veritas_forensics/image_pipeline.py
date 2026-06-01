from __future__ import annotations

import io
import math
from pathlib import Path
from typing import Any

import numpy as np
from PIL import ExifTags, Image, ImageChops, ImageEnhance, ImageFilter, UnidentifiedImageError

from . import __version__
from .common import artifact, ensure_dir, finding, sha256_bytes, sha256_file, write_json

JPEG_MARKERS = {
    0xC0: "SOF0",
    0xC2: "SOF2",
    0xC4: "DHT",
    0xD8: "SOI",
    0xD9: "EOI",
    0xDA: "SOS",
    0xDB: "DQT",
    0xDD: "DRI",
    0xE0: "APP0",
    0xE1: "APP1",
    0xE2: "APP2",
    0xEE: "APP14",
    0xFE: "COM",
}


def analyze_image(input_path: Path, out_dir: Path, json_path: Path) -> dict[str, Any]:
    ensure_dir(out_dir)
    artifacts: list[dict[str, str]] = []
    findings: list[dict[str, str]] = []
    payload: dict[str, Any] = {
        "tool": "veritas_forensics",
        "tool_version": __version__,
        "pipeline": "image",
        "input": str(input_path),
        "sha256": sha256_file(input_path),
        "artifacts": artifacts,
        "findings": findings,
    }

    data = input_path.read_bytes()
    payload["file_size_bytes"] = len(data)
    payload["jpeg_markers"] = scan_jpeg_markers(data)

    try:
        with Image.open(input_path) as image:
            image.load()
            payload["mime"] = Image.MIME.get(image.format or "", "application/octet-stream")
            payload["format"] = image.format
            payload["dimensions"] = {"width": image.width, "height": image.height}
            payload["mode"] = image.mode
            payload["pil_info"] = compact_info(image.info)
            payload["exif"] = extract_exif(image)
            payload["icc_profile"] = icc_profile_info(image)
            payload["jpeg_quantization"] = quantization_info(image)
            payload["jpeg_quality_estimate"] = estimate_quality(payload["jpeg_quantization"])
            payload["perceptual_hashes"] = perceptual_hashes(image)
            payload["recompression_sweep"] = recompression_sweep(image, data)
            payload["weak_cfa_periodicity"] = weak_cfa_periodicity(image)

            for q in (85, 90, 95):
                ela_path = out_dir / f"ela_q{q}.jpg"
                make_ela(image, q, ela_path)
                artifacts.append(artifact(ela_path, out_dir, "ela"))

            boundary_path = out_dir / "jpeg_8x8_boundary_map.png"
            make_boundary_map(image, boundary_path)
            artifacts.append(artifact(boundary_path, out_dir, "jpeg-boundary-map"))

            residual_path = out_dir / "median_residual_map.png"
            make_median_residual(image, residual_path)
            artifacts.append(artifact(residual_path, out_dir, "median-residual"))

            fft_path = out_dir / "fft_spectrum.png"
            make_fft_spectrum(image, fft_path)
            artifacts.append(artifact(fft_path, out_dir, "fft-spectrum"))

            payload["orb_copy_move_triage"] = optional_orb_triage(image)
    except UnidentifiedImageError:
        payload["summary"] = "The file could not be decoded as an image. Hashing and byte-level marker scans were still recorded."
        findings.append(finding(
            category="Metadata",
            claim="The uploaded file could not be decoded by Pillow as an image.",
            confidence="High",
            direction="Inconclusive",
            evidence="Image decoding failed before metadata extraction.",
            limitations="This can happen for corrupt files, unsupported formats, or mislabeled uploads. It does not establish manipulation or AI generation.",
            falsification_path="Provide the platform-original media file or a different export and repeat analysis.",
        ))
        write_json(json_path, payload)
        return payload

    exif = payload.get("exif") or {}
    if exif.get("tag_count", 0) == 0:
        findings.append(finding(
            category="Metadata",
            claim="The file has no EXIF metadata available to this worker.",
            confidence="High",
            direction="Neutral",
            evidence="No EXIF tags were returned by Pillow.",
            limitations="Social platforms, messengers, editors, screenshots, and reposts commonly strip EXIF. This is not evidence of AI generation.",
            falsification_path="Provide the original camera file or a platform original with capture metadata.",
        ))
    else:
        findings.append(finding(
            category="Metadata",
            claim="The file contains EXIF metadata that can support provenance review.",
            confidence="High",
            direction="Neutral",
            evidence=f"{exif.get('tag_count')} EXIF tags were extracted.",
            limitations="EXIF can be incomplete, stripped, rewritten, or fabricated and is not sufficient by itself for attribution.",
            falsification_path="Compare EXIF against original source files, platform originals, and source chronology.",
        ))

    if payload.get("jpeg_quantization", {}).get("table_count", 0) > 0:
        findings.append(finding(
            category="Compression",
            claim="JPEG quantization and recompression features were extracted for comparison.",
            confidence="Medium",
            direction="Neutral",
            evidence=f"Estimated quality: {payload.get('jpeg_quality_estimate', {}).get('quality', 'unknown')}; recompression sweep saved numeric deltas.",
            limitations="Compression fingerprints are affected by platforms and editors and cannot prove AI generation.",
            falsification_path="Compare against platform-original and earlier copies of the same media.",
        ))

    findings.append(finding(
        category="ELA",
        claim="ELA, boundary, residual, and FFT artifacts were generated for manual inspection.",
        confidence="Low",
        direction="Inconclusive",
        evidence="Visual artifacts were written next to the JSON report.",
        limitations="ELA and residual maps are triage aids. Recompression, resizing, screenshots, and platform exports can create misleading patterns.",
        falsification_path="Inspect artifacts against original media and known platform recompression behavior.",
    ))

    payload["summary"] = "Image analysis completed. Results are conservative triage signals and do not establish AI generation or attribution."
    write_json(json_path, payload)
    return payload


def compact_info(info: dict[str, Any]) -> dict[str, Any]:
    compact: dict[str, Any] = {}
    for key, value in info.items():
        if isinstance(value, bytes):
            compact[key] = {"byte_length": len(value), "sha256": sha256_bytes(value)}
        elif isinstance(value, (str, int, float, bool)):
            compact[key] = value
        else:
            compact[key] = str(type(value).__name__)
    return compact


def extract_exif(image: Image.Image) -> dict[str, Any]:
    exif = image.getexif()
    tags: dict[str, str] = {}
    for key, value in exif.items():
        name = ExifTags.TAGS.get(key, str(key))
        if isinstance(value, bytes):
            tags[name] = f"<{len(value)} bytes sha256={sha256_bytes(value)}>"
        else:
            tags[name] = str(value)
    return {"tag_count": len(tags), "tags": tags}


def icc_profile_info(image: Image.Image) -> dict[str, Any]:
    profile = image.info.get("icc_profile")
    if not profile:
        return {"present": False}
    return {"present": True, "byte_length": len(profile), "sha256": sha256_bytes(profile)}


def scan_jpeg_markers(data: bytes) -> list[dict[str, Any]]:
    markers: list[dict[str, Any]] = []
    i = 0
    while i < len(data) - 1:
        if data[i] != 0xFF:
            i += 1
            continue
        while i < len(data) - 1 and data[i + 1] == 0xFF:
            i += 1
        marker = data[i + 1]
        if marker == 0x00:
            i += 2
            continue
        name = JPEG_MARKERS.get(marker, f"0xFF{marker:02X}")
        markers.append({"offset": i, "marker": f"0xFF{marker:02X}", "name": name})
        i += 2
    return markers


def quantization_info(image: Image.Image) -> dict[str, Any]:
    tables = getattr(image, "quantization", None) or {}
    return {
        "table_count": len(tables),
        "tables": {str(k): list(v)[:64] for k, v in tables.items()},
    }


def estimate_quality(quantization: dict[str, Any]) -> dict[str, Any]:
    tables = quantization.get("tables", {})
    if not tables:
        return {"quality": None, "method": "not-jpeg-or-no-quantization"}
    first = next(iter(tables.values()))
    avg = sum(first) / max(1, len(first))
    quality = max(1, min(100, round(115 - avg * 1.15)))
    return {"quality": quality, "method": "rough quantization-table average heuristic"}


def recompression_sweep(image: Image.Image, original_bytes: bytes) -> list[dict[str, Any]]:
    original = np.asarray(image.convert("RGB"), dtype=np.float32)
    results: list[dict[str, Any]] = []
    for q in (75, 85, 90, 95):
        buffer = io.BytesIO()
        image.convert("RGB").save(buffer, format="JPEG", quality=q)
        buffer.seek(0)
        with Image.open(buffer) as recompressed:
            arr = np.asarray(recompressed.convert("RGB"), dtype=np.float32)
        mse = float(np.mean((original - arr) ** 2))
        results.append({
            "quality": q,
            "size_bytes": len(buffer.getvalue()),
            "size_delta_from_original": len(buffer.getvalue()) - len(original_bytes),
            "mse": mse,
        })
    return results


def perceptual_hashes(image: Image.Image) -> dict[str, str | None]:
    gray = image.convert("L").resize((8, 8))
    arr = np.asarray(gray)
    avg = arr.mean()
    ahash = "".join("1" if pixel > avg else "0" for pixel in arr.flatten())

    small = image.convert("L").resize((9, 8))
    d = np.asarray(small)
    dhash = "".join("1" if d[y, x] > d[y, x + 1] else "0" for y in range(8) for x in range(8))

    phash: str | None = None
    try:
        import imagehash
        phash = str(imagehash.phash(image))
    except Exception:
        phash = None

    return {"aHash_bits": ahash, "dHash_bits": dhash, "pHash": phash}


def make_ela(image: Image.Image, quality: int, output: Path) -> None:
    buffer = io.BytesIO()
    rgb = image.convert("RGB")
    rgb.save(buffer, "JPEG", quality=quality)
    buffer.seek(0)
    with Image.open(buffer) as recompressed:
        diff = ImageChops.difference(rgb, recompressed.convert("RGB"))
    extrema = diff.getextrema()
    max_diff = max(channel[1] for channel in extrema) or 1
    scale = min(255 / max_diff, 20)
    ImageEnhance.Brightness(diff).enhance(scale).save(output, "JPEG", quality=92)


def make_boundary_map(image: Image.Image, output: Path) -> None:
    arr = np.asarray(image.convert("L"), dtype=np.float32)
    result = np.zeros_like(arr)
    result[:, 8::8] = np.abs(arr[:, 8::8] - arr[:, 7:-1:8])
    result[8::8, :] = np.maximum(result[8::8, :], np.abs(arr[8::8, :] - arr[7:-1:8, :]))
    save_norm(result, output)


def make_median_residual(image: Image.Image, output: Path) -> None:
    gray = image.convert("L")
    median = gray.filter(ImageFilter.MedianFilter(size=3))
    residual = ImageChops.difference(gray, median)
    residual.save(output)


def make_fft_spectrum(image: Image.Image, output: Path) -> None:
    arr = np.asarray(image.convert("L"), dtype=np.float32)
    spectrum = np.fft.fftshift(np.fft.fft2(arr))
    magnitude = np.log(np.abs(spectrum) + 1)
    save_norm(magnitude, output)


def save_norm(arr: np.ndarray, output: Path) -> None:
    arr = arr.astype(np.float32)
    arr -= arr.min()
    max_value = arr.max()
    if max_value > 0:
        arr = arr / max_value
    Image.fromarray(np.uint8(arr * 255)).save(output)


def weak_cfa_periodicity(image: Image.Image) -> dict[str, Any]:
    arr = np.asarray(image.convert("L"), dtype=np.float32)
    if min(arr.shape) < 16:
        return {"available": False, "reason": "image too small"}
    residues = [float(arr[y::2, x::2].mean()) for y in range(2) for x in range(2)]
    spread = max(residues) - min(residues)
    return {
        "available": True,
        "residue_means": residues,
        "spread": spread,
        "interpretation": "weak periodicity heuristic only; absence or presence is not sufficient for camera-origin or AI claims",
    }


def optional_orb_triage(image: Image.Image) -> dict[str, Any]:
    try:
        import cv2
    except Exception:
        return {"available": False, "reason": "opencv-python is not installed"}

    arr = np.asarray(image.convert("L"))
    orb = cv2.ORB_create(nfeatures=800)
    keypoints, descriptors = orb.detectAndCompute(arr, None)
    return {
        "available": True,
        "keypoint_count": len(keypoints or []),
        "descriptor_shape": list(descriptors.shape) if descriptors is not None else None,
        "interpretation": "ORB keypoints are copy-move triage inputs, not proof of manipulation.",
    }
