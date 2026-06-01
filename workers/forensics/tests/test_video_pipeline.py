from __future__ import annotations

from pathlib import Path

from veritas_forensics.video_pipeline import scan_strings, summarize_ffprobe


def test_string_scan_detects_configured_markers(tmp_path: Path) -> None:
    sample = tmp_path / "sample.bin"
    sample.write_bytes(b"header TikTok metadata CapCut aweme footer")

    result = scan_strings(sample)

    assert result["matches"]["TikTok"] == 1
    assert result["matches"]["CapCut"] == 1
    assert result["matches"]["aweme"] == 1


def test_ffprobe_summary_shape() -> None:
    ffprobe = {
        "available": True,
        "format": {"format_name": "mov,mp4,m4a,3gp,3g2,mj2", "duration": "3.5", "tags": {"encoder": "test"}},
        "streams": [
            {"codec_type": "video", "codec_name": "h264", "width": 1920, "height": 1080, "avg_frame_rate": "30/1"},
            {"codec_type": "audio", "codec_name": "aac"},
        ],
    }

    summary = summarize_ffprobe(ffprobe)

    assert summary["duration_seconds"] == 3.5
    assert summary["video_codec"] == "h264"
    assert summary["audio_codec"] == "aac"
    assert summary["frame_rate"] == "30/1"
