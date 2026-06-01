SHELL := powershell.exe
.SHELLFLAGS := -NoProfile -ExecutionPolicy Bypass -Command

.PHONY: help install build test verify dev-up dev-down dev-health docker-up docker-down docker-health docker-logs compose-config clean-dev

help:
	@Write-Host "Veritas Workbench targets:"
	@Write-Host "  make install        Restore .NET, Python worker, and web dependencies"
	@Write-Host "  make build          Build backend and frontend"
	@Write-Host "  make test           Run backend, worker, and frontend tests"
	@Write-Host "  make verify         Run tests plus Docker Compose config validation"
	@Write-Host "  make dev-up         Start non-Docker API (SQLite) and Vite UI"
	@Write-Host "  make dev-health     Check non-Docker API and UI health"
	@Write-Host "  make dev-down       Stop non-Docker API and UI"
	@Write-Host "  make docker-up      Build and start Docker Compose stack"
	@Write-Host "  make docker-health  Check Docker API and UI health"
	@Write-Host "  make docker-down    Stop Docker Compose stack"
	@Write-Host "  make clean-dev      Stop local dev and remove generated local SQLite/log files"

install:
	dotnet tool restore
	dotnet restore Veritas.Workbench.slnx
	Push-Location workers/forensics; python -m pip install -e ".[dev]"; Pop-Location
	Push-Location apps/web; npm install; Pop-Location

build:
	dotnet build Veritas.Workbench.slnx --no-restore
	Push-Location apps/web; npm run build; Pop-Location

test:
	dotnet test Veritas.Workbench.slnx
	Push-Location workers/forensics; python -m pytest; Pop-Location
	Push-Location apps/web; npm test; npm run build; Pop-Location

verify: test compose-config

dev-up:
	& "$(CURDIR)/scripts/dev-up.ps1"

dev-down:
	& "$(CURDIR)/scripts/dev-down.ps1"

dev-health:
	& "$(CURDIR)/scripts/wait-health.ps1" -ApiUrl "http://127.0.0.1:8080/api/health" -WebUrl "http://127.0.0.1:5173" -TimeoutSeconds 15

docker-up:
	& "$(CURDIR)/scripts/docker-preflight.ps1" -TimeoutSeconds 30
	docker compose up --build -d
	& "$(CURDIR)/scripts/wait-health.ps1" -ApiUrl "http://127.0.0.1:8080/api/health" -WebUrl "http://127.0.0.1:5173" -TimeoutSeconds 180

docker-down:
	& "$(CURDIR)/scripts/docker-preflight.ps1" -TimeoutSeconds 30
	docker compose down --remove-orphans

docker-health:
	& "$(CURDIR)/scripts/wait-health.ps1" -ApiUrl "http://127.0.0.1:8080/api/health" -WebUrl "http://127.0.0.1:5173" -TimeoutSeconds 30

docker-logs:
	docker compose logs --tail=120

compose-config:
	docker compose config --quiet

clean-dev:
	& "$(CURDIR)/scripts/dev-down.ps1" -Quiet
	Remove-Item -LiteralPath "data/veritas-dev.db","data/veritas-dev.db-shm","data/veritas-dev.db-wal" -Force -ErrorAction SilentlyContinue
	Remove-Item -LiteralPath "data/logs" -Recurse -Force -ErrorAction SilentlyContinue
