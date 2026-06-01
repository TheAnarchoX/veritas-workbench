from __future__ import annotations

import argparse
from pathlib import Path

from .image_pipeline import analyze_image
from .video_pipeline import analyze_video


def main() -> int:
    parser = argparse.ArgumentParser(prog="veritas_forensics")
    sub = parser.add_subparsers(dest="command", required=True)

    image = sub.add_parser("analyze-image")
    image.add_argument("--input", required=True)
    image.add_argument("--out", required=True)
    image.add_argument("--json", required=True)

    video = sub.add_parser("analyze-video")
    video.add_argument("--input", required=True)
    video.add_argument("--out", required=True)
    video.add_argument("--json", required=True)
    video.add_argument("--fps", type=float, default=1.0)

    args = parser.parse_args()
    input_path = Path(args.input)
    out_dir = Path(args.out)
    json_path = Path(args.json)

    if args.command == "analyze-image":
        analyze_image(input_path, out_dir, json_path)
    elif args.command == "analyze-video":
        analyze_video(input_path, out_dir, json_path, fps=args.fps)
    else:
        parser.error("Unknown command")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
