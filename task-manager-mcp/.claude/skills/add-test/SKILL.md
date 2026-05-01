---
description: Create pytest test skeletons for changed API or MCP functionality.
---

You are adding pytest tests for this project.

Steps:
1. Identify the target module from the recent change or from what the user specifies. Focus on `api/` (C# — write Python integration tests in `tests/test_api.py`) or `mcp_server/` (write contract tests in `tests/test_mcp_contract.py`).
2. Read the existing test file to understand current coverage and fixture style.
3. For each changed or new function/endpoint, add tests covering:
   - **Happy path** — valid input returns expected result.
   - **Auth/validation path** — missing key returns 401, invalid field returns 422 or 400.
   - **Error path** — not-found returns 404, duplicate or constraint violation returns appropriate status.
4. Keep fixtures deterministic: use unique titles (e.g. `"test-add-test-<uuid>"`), never rely on existing database state.
5. Do not add external network calls in contract tests — use `unittest.mock` or `pytest` monkeypatch.
6. After writing the tests, run `C:\Users\l.zahorskaya\.local\bin\uv.exe run pytest tests/ -q` and confirm they pass.

Constraints:
- Tests must be runnable with `uv run pytest` from the `task-manager-mcp/` directory.
- Do not modify production code to make tests pass — fix the test instead.
- Do not delete existing passing tests.
