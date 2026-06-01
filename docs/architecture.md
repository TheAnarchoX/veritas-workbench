# Architecture

Veritas Workbench is a local-first monorepo:

- `apps/api`: ASP.NET Core Web API with EF Core persistence.
- `apps/web`: React, TypeScript, Vite, and Tailwind UI.
- `workers/forensics`: Python CLI for image and video forensic triage.
- `data/storage`: local filesystem storage for originals, artifacts, and reports.

The API owns projects, dossiers, sources, evidence items, analysis runs, findings, claims, tasks, timeline entries, and chain-of-custody events. PostgreSQL is the production database. Tests use SQLite.

Original uploads are stored immutably under `data/storage/originals`. Worker outputs are stored under `data/storage/artifacts`. Storage is behind `IEvidenceStorage` and can later be backed by MinIO, S3, or Azure Blob.

For MVP job execution, API endpoints create `AnalysisRun` records in `Pending` state. A hosted background service picks up pending runs, invokes:

```bash
python -m veritas_forensics analyze-image --input <file> --out <artifacts> --json <result>
python -m veritas_forensics analyze-video --input <file> --out <artifacts> --json <result>
```

The API imports the worker JSON, records artifacts, and creates conservative findings.
