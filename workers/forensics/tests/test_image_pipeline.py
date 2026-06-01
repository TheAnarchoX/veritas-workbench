from __future__ import annotations

from pathlib import Path

from PIL import Image

from veritas_forensics.image_pipeline import analyze_image, scan_jpeg_markers


def make_sample_jpeg(path: Path) -> None:
    image = Image.new("RGB", (64, 64), color=(120, 40, 80))
    image.save(path, "JPEG", quality=90)


def test_image_metadata_and_json_shape(tmp_path: Path) -> None:
    sample = tmp_path / "sample.jpg"
    out_dir = tmp_path / "artifacts"
    result = tmp_path / "result.json"
    make_sample_jpeg(sample)

    payload = analyze_image(sample, out_dir, result)

    assert payload["pipeline"] == "image"
    assert payload["dimensions"] == {"width": 64, "height": 64}
    assert payload["sha256"]
    assert payload["findings"]
    assert result.exists()


def test_jpeg_marker_scan_finds_soi(tmp_path: Path) -> None:
    sample = tmp_path / "sample.jpg"
    make_sample_jpeg(sample)

    markers = scan_jpeg_markers(sample.read_bytes())

    assert any(marker["name"] == "SOI" for marker in markers)
    assert any(marker["name"] == "DQT" for marker in markers)


def test_ela_and_recompression_outputs_exist(tmp_path: Path) -> None:
    sample = tmp_path / "sample.jpg"
    out_dir = tmp_path / "artifacts"
    result = tmp_path / "result.json"
    make_sample_jpeg(sample)

    payload = analyze_image(sample, out_dir, result)

    assert (out_dir / "ela_q85.jpg").exists()
    assert (out_dir / "ela_q90.jpg").exists()
    assert (out_dir / "ela_q95.jpg").exists()
    assert len(payload["recompression_sweep"]) == 4
    assert {"quality", "size_bytes", "mse"}.issubset(payload["recompression_sweep"][0].keys())
