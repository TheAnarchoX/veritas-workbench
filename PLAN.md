# Veritas Workbench MVP Plan

## Ground Rules

- [x] Preserve provenance-first, falsification-oriented product language.
- [x] Do not add face recognition, doxxing, harassment, stalking, deanonymization, or scraping-bypass workflows.
- [x] Keep findings conservative with confidence, evidence basis, limitations, and falsification paths.
- [x] Respect robots.txt and manual-source fallback requirements.

## Implementation

- [x] Create repository structure and shared configuration.
- [x] Create ASP.NET Core solution with Domain, Application, Infrastructure, API, and Tests projects.
- [x] Implement EF Core PostgreSQL persistence with migrations.
- [x] Implement domain entities and enums for projects, dossiers, sources, evidence, analysis, findings, claims, tasks, custody events, and timeline entries.
- [x] Implement local filesystem evidence storage abstraction.
- [x] Implement project, dossier, source, evidence, analysis, finding, claim, task, and report REST APIs.
- [x] Implement robots.txt policy service with conservative fallback and cacheable decisions.
- [x] Implement database-backed analysis job runner and Python worker invocation.
- [x] Implement Markdown report generation.
- [x] Implement Python forensic worker CLI with image analysis pipeline.
- [x] Implement Python forensic worker CLI with video analysis pipeline.
- [x] Implement worker JSON outputs, artifact generation, and conservative finding summaries.
- [x] Implement React/Vite/Tailwind UI for projects, dossiers, evidence, source intake, findings, tasks, claims, timeline, reports, and settings.
- [x] Add synthetic demo seed data.
- [x] Add required documentation and AGENTS.md.
- [x] Add Docker Compose for PostgreSQL, API, and web.

## Verification

- [x] Backend tests cover project creation, dossier creation, evidence upload, source URL flow, robots decisions, analysis transitions, and report generation.
- [x] Worker tests cover image metadata, JPEG marker scan, ELA artifact creation, recompression result shape, video parsing/string scan, and JSON schema shape.
- [x] Frontend tests cover project list, dossier evidence tab, upload validation, confidence badge, and report page.
- [x] Run backend tests.
- [x] Run worker tests.
- [x] Run frontend tests/build.
- [x] Run Docker Compose configuration validation.
