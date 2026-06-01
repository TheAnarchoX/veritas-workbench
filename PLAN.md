# Veritas Workbench MVP Plan

## Ground Rules

- [ ] Preserve provenance-first, falsification-oriented product language.
- [ ] Do not add face recognition, doxxing, harassment, stalking, deanonymization, or scraping-bypass workflows.
- [ ] Keep findings conservative with confidence, evidence basis, limitations, and falsification paths.
- [ ] Respect robots.txt and manual-source fallback requirements.

## Implementation

- [ ] Create repository structure and shared configuration.
- [ ] Create ASP.NET Core solution with Domain, Application, Infrastructure, API, and Tests projects.
- [ ] Implement EF Core PostgreSQL persistence with migrations.
- [ ] Implement domain entities and enums for projects, dossiers, sources, evidence, analysis, findings, claims, tasks, custody events, and timeline entries.
- [ ] Implement local filesystem evidence storage abstraction.
- [ ] Implement project, dossier, source, evidence, analysis, finding, claim, task, and report REST APIs.
- [ ] Implement robots.txt policy service with conservative fallback and cacheable decisions.
- [ ] Implement database-backed analysis job runner and Python worker invocation.
- [ ] Implement Markdown report generation.
- [ ] Implement Python forensic worker CLI with image analysis pipeline.
- [ ] Implement Python forensic worker CLI with video analysis pipeline.
- [ ] Implement worker JSON outputs, artifact generation, and conservative finding summaries.
- [ ] Implement React/Vite/Tailwind UI for projects, dossiers, evidence, source intake, findings, tasks, claims, timeline, reports, and settings.
- [ ] Add synthetic demo seed data.
- [ ] Add required documentation and AGENTS.md.
- [ ] Add Docker Compose for PostgreSQL, API, and web.

## Verification

- [ ] Backend tests cover project creation, dossier creation, evidence upload, source URL flow, robots decisions, analysis transitions, and report generation.
- [ ] Worker tests cover image metadata, JPEG marker scan, ELA artifact creation, recompression result shape, video parsing/string scan, and JSON schema shape.
- [ ] Frontend tests cover project list, dossier evidence tab, upload validation, confidence badge, and report page.
- [ ] Run backend tests.
- [ ] Run worker tests.
- [ ] Run frontend tests/build.
- [ ] Run Docker Compose configuration validation.
