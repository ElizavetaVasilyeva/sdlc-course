# Task Manager MCP

A task management system exposed to Claude Code via the Model Context Protocol (MCP).

## Architecture

```
Claude Code
    │  MCP stdio transport
    ▼
mcp_server/server.py      Python FastMCP — tools, resources, prompts
    │  httpx async HTTP
    ▼
ASP.NET Core Minimal API  http://127.0.0.1:8000
    │  Entity Framework Core
    ▼
SQLite  task_manager.db
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Python 3.11+](https://www.python.org/downloads/) with [uv](https://docs.astral.sh/uv/getting-started/installation/)
- [Node.js / npm](https://nodejs.org/) — for MCP Inspector only
- [Claude Code](https://claude.ai/code)

## Setup

```powershell
# 1. Clone / open the repo
cd task-manager-mcp

# 2. Copy environment template
cp .env.example .env
# Edit .env and set TASK_API_KEY to a value of your choice

# 3. Install Python dependencies
uv sync
```

## Starting the API

```powershell
cd api
dotnet run
# API is now listening on http://127.0.0.1:8000
```

Verify:
```powershell
curl http://127.0.0.1:8000/health
# {"status":"ok"}
```

Scalar UI (interactive docs): http://127.0.0.1:8000/scalar/v1

## Seed tasks (optional)

```powershell
$key = "dev-task-manager-key-change-me"
$base = "http://127.0.0.1:8000"

Invoke-RestMethod -Method Post -Uri "$base/tasks" `
  -Headers @{"X-API-Key"=$key} `
  -ContentType "application/json" `
  -Body '{"title":"Fix critical bug","priority":5,"status":"in-progress","due_date":"2026-05-01"}'

Invoke-RestMethod -Method Post -Uri "$base/tasks" `
  -Headers @{"X-API-Key"=$key} `
  -ContentType "application/json" `
  -Body '{"title":"Write unit tests","priority":3,"status":"completed"}'

Invoke-RestMethod -Method Post -Uri "$base/tasks" `
  -Headers @{"X-API-Key"=$key} `
  -ContentType "application/json" `
  -Body '{"title":"Update documentation","priority":2,"status":"todo","due_date":"2026-05-10"}'
```

## Running tests

```powershell
# C# integration tests (no live API needed)
dotnet test

# Python MCP contract tests (no live API needed)
uv run pytest tests/test_mcp_contract.py -q

# Python API integration tests (API must be running)
uv run pytest tests/test_api.py -q
```

## Lint, format, type check

```powershell
uv run ruff check .
uv run black --check .
uv run mypy mcp_server
```

## MCP Inspector

```powershell
$env:TASK_API_KEY = "dev-task-manager-key-change-me"
$env:TASK_API_BASE_URL = "http://127.0.0.1:8000"
npx -y @modelcontextprotocol/inspector uv run python -m mcp_server.server
```

Open `http://127.0.0.1:6274`, click **Connect**, then check Tools / Resources / Prompts tabs.

## Claude Code integration

The `.mcp.json` at the project root registers the `task_manager` MCP server automatically.

```
/mcp                                      verify server is connected
/mcp__task_manager__daily_plan            today's top-priority tasks
/mcp__task_manager__prioritize_tasks      suggest priority order for open tasks
```

## MCP capabilities

### Tools

| Tool | Description |
|------|-------------|
| `get_task(task_id)` | Fetch a single task by ID |
| `get_all_tasks(status?, due_date?)` | List tasks with optional filters |
| `add_task(title, priority, ...)` | Create a new task |
| `update_task(task_id, ...)` | Update one or more fields |
| `delete_task(task_id)` | Permanently delete a task |

### Resources

| URI | Content |
|-----|---------|
| `tasks://all` | All tasks as JSON |
| `tasks://completed` | Completed tasks |
| `tasks://today` | Tasks due today |
| `tasks://in-progress` | In-progress tasks |

### Prompts

| Command | Behaviour |
|---------|-----------|
| `/mcp__task_manager__daily_plan` | Top 3 highest-priority tasks for today |
| `/mcp__task_manager__prioritize_tasks` | Ordered priority list for all open tasks |

## Skills

```
/git-commit   Generate a conventional commit message from git diff
/add-test     Scaffold pytest test cases for changed functionality
```

## Sub-agents

```
code-reviewer   Reviews auth, validation, MCP delegation, and secrets
test-writer     Creates or improves pytest tests
```

## Hooks

| Hook | Trigger | Action |
|------|---------|--------|
| PreToolUse | Edit / Write / MultiEdit | Blocks writes containing likely real secrets |
| PostToolUse | Edit / Write / MultiEdit | Runs ruff, black --check, pytest after changes |

## API reference

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/health` | No | Health check |
| GET | `/tasks` | Yes | List (optional `?status=` and `?due_date=`) |
| GET | `/tasks/{id}` | Yes | Get by ID |
| POST | `/tasks` | Yes | Create |
| PATCH | `/tasks/{id}` | Yes | Partial update |
| DELETE | `/tasks/{id}` | Yes | Delete |

Auth header: `X-API-Key: <your-key>`

## Troubleshooting

| Problem | Fix |
|---------|-----|
| 401 on every request | Check `TASK_API_KEY` in `.env` matches what the API loaded |
| API not running | `cd api && dotnet run` — check port 8000 is free |
| MCP connection failure | Ensure API is running; check `TASK_API_BASE_URL` in `.mcp.json` |
| `uv` not found | Use full path `C:\Users\<you>\.local\bin\uv.exe` |
| Inspector "Request timed out" | Set `TASK_API_KEY` env var before launching the inspector |

## Definition of Done

- [x] REST API: CRUD, API key auth, input validation, SQLite persistence
- [x] MCP server: 5 tools, 4 resources, 2 prompts
- [x] C# integration tests (xUnit + WebApplicationFactory)
- [x] Python integration tests (pytest + httpx)
- [x] Python MCP contract tests
- [x] MCP Inspector validation
- [x] Claude Code `.mcp.json` integration
- [x] `CLAUDE.md` project documentation
- [x] `/git-commit` and `/add-test` skills
- [x] `code-reviewer` and `test-writer` sub-agents
- [x] Pre-edit and post-edit hooks
- [x] This README
