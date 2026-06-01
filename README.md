# Veritas Workbench

Veritas Workbench is an open-source, local-first OSINT and media-forensics workspace for truth-seeking investigations into suspected AI-generated, AI-altered, stolen, reposted, or coordinated online media.

It is not an AI detector. It helps organize evidence, provenance, source chronology, cautious forensic triage, claims, tasks, chain-of-custody events, and Markdown reports.

## Ethical Boundaries

- No private-person face recognition.
- No harassment, doxxing, stalking, or deanonymization workflows.
- Findings must include confidence, evidence basis, limitations, and a falsification path.
- Collection is robots.txt-aware, API-first, rate-limit-sensitive, and manual-fallback by default.

## Local Development

Requirements:

- .NET SDK 9-compatible environment
- Node.js 24+
- Python 3.11+
- PostgreSQL, or Docker Compose
- Optional: `ffmpeg`/`ffprobe` for video analysis

The easiest local path is:

```bash
make install
make dev-up
```

This starts the API on http://127.0.0.1:8080 with SQLite under `data/veritas-dev.db` and the web UI on http://127.0.0.1:5173. Stop it with:

```bash
make dev-down
```

If GNU Make is not installed on Windows, run the underlying scripts directly:

```powershell
dotnet tool restore
dotnet restore Veritas.Workbench.slnx
Push-Location workers/forensics; python -m pip install -e ".[dev]"; Pop-Location
Push-Location apps/web; npm install; Pop-Location
.\scripts\dev-up.ps1
.\scripts\dev-down.ps1
```

Install and test the worker:

```bash
cd workers/forensics
python -m pip install -e ".[dev]"
python -m pytest
```

Run the API:

```bash
dotnet tool restore
dotnet build Veritas.Workbench.slnx
dotnet run --project apps/api/Veritas.Api
```

Run the web UI:

```bash
cd apps/web
npm install
npm run dev
```

Set `VITE_API_BASE_URL=http://localhost:8080/api` if your API is on port 8080.

## Docker Compose

```bash
make docker-up
```

Then open:

- Web UI: http://localhost:5173
- API: http://localhost:8080/api/health

Docker Compose starts PostgreSQL, applies EF migrations, seeds optional synthetic demo data, and mounts local storage under `data/storage`.
The Docker Make target runs a daemon preflight first so a broken Docker Desktop state fails quickly instead of hanging during Compose.

Stop Docker services with:

```bash
make docker-down
```

## Basic Workflow

1. Open `/projects`.
2. Create a project.
3. Open the project and create a dossier.
4. Open the dossier and use the Sources tab to add a URL.
5. For X/Twitter URLs, the app checks collection policy and requests manual original media or a `pbs.twimg.com` `name=orig` URL when automatic collection is not clearly allowed.
6. Use the Evidence tab to upload original media, screenshots, videos, archive captures, or metadata text.
7. Open an evidence item and run image or video analysis.
8. Review metadata, ELA outputs, JPEG/compression summary, residual/FFT artifacts, frames, corner crops, string scans, findings, tasks, and limitations.
9. Add claims and tasks.
10. Open the Report tab or `/dossiers/:id/report` to export Markdown.

## Tests

```bash
make test
```

## What This Is Not

Veritas Workbench does not identify private people, prove that a person exists or does not exist, or classify media as AI-generated from a single heuristic. It distinguishes missing provenance, AI-generation indicators, AI-alteration indicators, recompression, platform exports, persona-construction signals, and unsupported claims.
