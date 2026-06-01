# Contributing

## Development setup

Use the Makefile where available:

```bash
make install
make dev-up
make test
```

On Windows without GNU Make, run the scripts directly:

```powershell
dotnet restore Veritas.Workbench.slnx
Push-Location workers/forensics; python -m pip install -e ".[dev]"; Pop-Location
Push-Location apps/web; npm install; Pop-Location
.\scripts\dev-up.ps1
```

## Contribution standards

- Keep evidence, source, entity, task, finding, claim, and timeline workflows connected.
- Prefer cautious OSINT language over detector-style certainty.
- Add focused tests for API behavior, worker logic, and frontend workflow changes.
- Do not commit local evidence, generated artifacts, logs, SQLite databases, or screenshots from real investigations.
- Document user-facing changes in `README.md` when setup or workflow changes.

## Pull request checklist

- `dotnet test Veritas.Workbench.slnx`
- `cd workers/forensics && python -m pytest`
- `cd apps/web && npm test && npm run build`
- `docker compose config --quiet`

Docker runtime checks should be run when Docker Desktop or the local daemon is healthy.
