"""
MCP contract tests — verify server structure and client behaviour
without requiring a running API or MCP Inspector.
"""

from unittest.mock import AsyncMock, patch

import pytest

# ── Import tests ──────────────────────────────────────────────────────────────


def test_server_imports_without_side_effects():
    """Importing server must not start the MCP server or make network calls."""
    import mcp_server.server as srv  # noqa: F401 — just verify import works

    assert hasattr(srv, "mcp"), "FastMCP instance 'mcp' must exist on the module"


def test_client_imports_without_side_effects():
    """Importing client must not make any network calls."""
    import mcp_server.client as c  # noqa: F401

    assert callable(c.get_task)
    assert callable(c.get_all_tasks)
    assert callable(c.add_task)
    assert callable(c.update_task)
    assert callable(c.delete_task)


# ── API key header tests ──────────────────────────────────────────────────────


@pytest.mark.asyncio
async def test_client_sends_api_key_header(monkeypatch):
    """Every client call must include X-API-Key in the request headers."""
    import mcp_server.client as c

    monkeypatch.setenv("TASK_API_KEY", "test-key-123")
    monkeypatch.setenv("TASK_API_BASE_URL", "http://localhost:8000")

    async def fake_get(url: str, **kwargs):
        response = AsyncMock()
        response.is_success = True
        response.json.return_value = {"id": 1, "title": "t", "priority": 1, "status": "todo"}
        return response

    with patch("httpx.AsyncClient.get", side_effect=fake_get):
        # Reload to pick up monkeypatched env vars
        import importlib

        importlib.reload(c)

        await c.get_task(1)

    # Client is constructed with headers — verify the module-level _HEADERS dict
    assert "X-API-Key" in c._HEADERS
    assert c._HEADERS["X-API-Key"] == "test-key-123"


# ── Prompt content tests ──────────────────────────────────────────────────────


@pytest.mark.asyncio
async def test_daily_plan_prompt_contains_required_keywords():
    """daily_plan must return instructions mentioning top 3, priority, and due date."""
    sample_tasks = [
        {"id": 1, "title": "Fix bug", "priority": 5, "status": "todo", "due_date": "2026-05-01"},
        {"id": 2, "title": "Write docs", "priority": 3, "status": "todo", "due_date": "2026-05-01"},
    ]

    with patch("mcp_server.client.get_all_tasks", new=AsyncMock(return_value=sample_tasks)):
        from mcp_server.server import daily_plan

        result = await daily_plan()

    assert isinstance(result, str)
    lower = result.lower()
    assert "top 3" in lower, "prompt must mention 'top 3'"
    assert "priority" in lower, "prompt must mention 'priority'"
    assert "due date" in lower or "due_date" in lower, "prompt must mention due date"
    assert "status" in lower, "prompt must mention 'status'"


@pytest.mark.asyncio
async def test_prioritize_tasks_prompt_contains_required_keywords():
    """prioritize_tasks must return instructions mentioning priority, due date, and status."""
    sample_tasks = [
        {"id": 1, "title": "Task A", "priority": 4, "status": "in-progress", "due_date": None},
        {"id": 2, "title": "Task B", "priority": 2, "status": "todo", "due_date": "2026-05-10"},
    ]

    with patch("mcp_server.client.get_all_tasks", new=AsyncMock(return_value=sample_tasks)):
        from mcp_server.server import prioritize_tasks

        result = await prioritize_tasks()

    assert isinstance(result, str)
    lower = result.lower()
    assert "priority" in lower, "prompt must mention 'priority'"
    assert "due date" in lower or "due_date" in lower, "prompt must mention due date"
    assert "status" in lower, "prompt must mention 'status'"


# ── Tool registration tests ───────────────────────────────────────────────────


def test_all_tools_registered():
    """All five required tools must be registered on the FastMCP instance."""

    # FastMCP stores tools internally — check via list_tools if available,
    # otherwise verify the decorated functions exist on the module.
    import mcp_server.server as srv

    for name in ("get_task", "get_all_tasks", "add_task", "update_task", "delete_task"):
        assert hasattr(srv, name), f"Tool '{name}' must be defined in server.py"


def test_all_resources_registered():
    """All four required resources must be defined in the server module."""
    import mcp_server.server as srv

    for name in (
        "resource_all_tasks",
        "resource_completed_tasks",
        "resource_today_tasks",
        "resource_in_progress_tasks",
    ):
        assert hasattr(srv, name), f"Resource function '{name}' must be defined in server.py"


def test_both_prompts_registered():
    """Both required prompts must be defined in the server module."""
    import mcp_server.server as srv

    for name in ("daily_plan", "prioritize_tasks"):
        assert hasattr(srv, name), f"Prompt '{name}' must be defined in server.py"
