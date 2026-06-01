# Veritas Forensics Worker

This worker produces conservative image and video analysis artifacts for Veritas Workbench. It is not an AI detector. Outputs are intended to support provenance investigation, source chronology, and falsifiable claims.

Run image analysis:

```bash
python -m veritas_forensics analyze-image --input sample.jpg --out artifacts --json result.json
```

Run video analysis:

```bash
python -m veritas_forensics analyze-video --input sample.mp4 --out artifacts --json result.json
```

Optional tools:

- `ffprobe` and `ffmpeg` enable video metadata and frame extraction.
- `opencv-python` enables optional ORB/copy-move and optical-flow triage.
- `tesseract` is not required; the worker emits OCR-ready crops when video frames can be extracted.
