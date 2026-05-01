# Task Manager MCP — Claude Code Project Guide

## Architecture

```
Claude Code
    │  (MCP stdio transport)
    ▼
mcp_server/server.py   ← FastMCP tools / resources / prompts
    │  (httpx async)
    ▼
ASP.NET Core Minimal API  (http://127.0.0.1:8000)
    │  (Entity Framework Core)
    ▼
SQLite  (task_manager.db)
```

**Rule:** The MCP layer must never access the database directly. All data flows through the REST API.

## Project structure

```
task-manager-mcp/
├── api/                        C# ASP.NET Core Minimal API
│   ├── api.csproj
│   ├── Program.cs              endpoints + auth + validation
│   ├── Models.cs               TaskItem, DTOs, WorkStatus enum
│   └── AppDbContext.cs         EF Core + SQLite
├── api.Tests/                  xUnit integration tests (WebApplicationFactory)
│   ├── api.Tests.csproj
│   └── TaskApiTests.cs
├── mcp_server/
│   ├── server.py               FastMCP tools, resources, prompts
│   └── client.py               httpx async client → REST API
├── tests/
│   ├── test_api.py             Python integration tests (auto-skip if API offline)
│   └── test_mcp_contract.py   MCP import / contract tests (no live API needed)
├── scripts/
│   ├── check_secrets.py        pre-edit hook — blocks likely secrets
│   └── post_edit_quality.py    post-edit hook — ruff + black + pytest
├── .claude/
│   ├── settings.json           hook configuration
│   ├── skills/
│   │   ├── git-commit/SKILL.md
│   │   └── add-test/SKILL.md
│   └── agents/
│       ├── code-reviewer.md
│       └── test-writer.md
├── .mcp.json                   MCP server registration for Claude Code
├── .env                        local secrets (never commit)
├── .env.example                placeholder values (committed)
├── pyproject.toml
└── CLAUDE.md                   this file
```

## API key rules

- Never commit real API keys.
- Store `TASK_API_KEY` in `.env` locally; `.env.example` holds placeholders.
- The C# API reads the key from the environment and compares it using constant-time comparison.
- Every `/tasks` request must include the header `X-API-Key: <key>`.
- Health check `GET /health` is public (no key required).

## Running the system

```powershell
# 1. Start the C# REST API (keep this terminal open)
cd task-manager-mcp/api
dotnet run

# 2. Run C# tests (separate terminal)
cd task-manager-mcp
dotnet test

# 3. Run Python MCP contract tests (no live API needed)
$env:USERPROFILE\.local\bin\uv.exe run pytest tests/test_mcp_contract.py -q

# 4. Run Python integration tests (API must be running)
$env:USERPROFILE\.local\bin\uv.exe run pytest tests/test_api.py -q

# 5. Lint and format check
$env:USERPROFILE\.local\bin\uv.exe run ruff check .
$env:USERPROFILE\.local\bin\uv.exe run black --check .

# 6. MCP Inspector (validate tools/resources/prompts manually)
$env:TASK_API_KEY="dev-task-manager-key-change-me"
$env:TASK_API_BASE_URL="http://127.0.0.1:8000"
npx -y @modelcontextprotocol/inspector C:\Users\<you>\.local\bin\uv.exe run python -m mcp_server.server
```

## Claude Code MCP connection

The `.mcp.json` at the workspace root registers the `task_manager` MCP server automatically.  
Claude Code prompts appear as:
- `/mcp__task_manager__daily_plan`
- `/mcp__task_manager__prioritize_tasks`

## Coding standards

- All Python functions must have type annotations.
- MCP tools/resources must delegate to `mcp_server/client.py`; no direct DB access.
- Validation errors: 422 for schema violations, 400 for invalid enum values, 401 for auth failures.
- New endpoints require matching tests in both C# (`api.Tests/`) and Python (`tests/test_api.py`).
- Run `ruff check .` and `black --check .` before committing.

## Skills

| Slash command | When to use |
|---|---|
| `/git-commit` | After any code change — generates a conventional commit message from `git diff` |
| `/add-test` | After adding or modifying API/MCP functionality — scaffolds pytest test cases |

## Sub-agents

| Agent | When to use |
|---|---|
| `code-reviewer` | Delegate after completing a feature — checks auth, validation, MCP delegation, no secrets |
| `test-writer` | Delegate when test coverage is thin — creates focused pytest tests |

## Hooks

- **PreToolUse** (`Edit` / `Write` / `MultiEdit`): `scripts/check_secrets.py` scans the content being written and blocks if a likely real secret is detected.
- **PostToolUse** (`Edit` / `Write` / `MultiEdit`): `scripts/post_edit_quality.py` runs ruff, black --check, and pytest after every file change.

## Review workflow

This project follows sequential approval gates. Complete one step fully, verify it passes, then proceed to the next. Do not combine steps or skip gates.
