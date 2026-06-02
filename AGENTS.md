# Instructions for Future Coding Agents

- For the local non-Docker dev stack, always use `powershell -ExecutionPolicy Bypass -File .\scripts\dev-up.ps1 -NoBuild` to restart services. Do not start `Veritas.Api` with bare `dotnet run` or `Veritas.Api.exe`; the dev script sets the required SQLite runtime (`Database__Provider=Sqlite`, `ConnectionStrings__Sqlite=data/veritas-dev.db`, `Database__EnsureCreated=true`) and `VITE_API_BASE_URL`.
- After restarting the API, verify `http://127.0.0.1:8080/api/projects`, not only `/api/health`; health does not prove database connectivity.
- Preserve the ethical constraints: no doxxing, harassment, stalking, deanonymization, or private-person face recognition.
- Keep the product provenance-first. Do not reposition it as an AI detector.
- Use conservative forensic language. Findings need confidence, evidence, limitations, and falsification paths.
- Do not add scraping bypasses, login-wall bypasses, anti-bot bypasses, paywall bypasses, or terms-hostile collection paths.
- Prefer official APIs and manual upload fallbacks when robots.txt, terms, or technical boundaries are ambiguous.
- Add tests for new API workflows and worker pipeline changes.
- Preserve immutable originals. Replacements should become new evidence items linked by source or dossier context.
- Keep docs updated when collection policy, forensic methods, storage, or job behavior changes.
