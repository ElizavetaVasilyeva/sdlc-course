"""
Integration tests for the Task Manager API.
Requires the API to be running at TASK_API_BASE_URL (default: http://127.0.0.1:8000).
Start it with: cd task-manager-mcp/api && dotnet run
Tests are skipped automatically if the server is not reachable.
"""

import os

import httpx
import pytest

BASE_URL = os.getenv("TASK_API_BASE_URL", "http://127.0.0.1:8000")
API_KEY = os.getenv("TASK_API_KEY", "dev-task-manager-key-change-me")
AUTH = {"X-API-Key": API_KEY}


@pytest.fixture(scope="session")
def client():
    c = httpx.Client(base_url=BASE_URL, timeout=5.0)
    try:
        c.get("/health")
    except (httpx.ConnectError, httpx.TimeoutException):
        c.close()
        pytest.skip(f"API not running at {BASE_URL}")
    yield c
    c.close()


def _create(client, title="Test Task", priority=3, status="todo", due_date=None):
    payload = {"title": title, "priority": priority, "status": status}
    if due_date:
        payload["due_date"] = due_date
    r = client.post("/tasks", json=payload, headers=AUTH)
    r.raise_for_status()
    return r.json()


# ── Health ────────────────────────────────────────────────────────────────────


def test_health_no_auth(client):
    r = client.get("/health")
    assert r.status_code == 200
    assert r.json()["status"] == "ok"


# ── Authentication ────────────────────────────────────────────────────────────


def test_tasks_missing_key_returns_401(client):
    r = client.get("/tasks")
    assert r.status_code == 401


def test_tasks_invalid_key_returns_401(client):
    r = client.get("/tasks", headers={"X-API-Key": "wrong"})
    assert r.status_code == 401


# ── Create ────────────────────────────────────────────────────────────────────


def test_create_task_returns_201(client):
    r = client.post(
        "/tasks", json={"title": "py task", "priority": 3, "status": "todo"}, headers=AUTH
    )
    assert r.status_code == 201
    data = r.json()
    assert data["title"] == "py task"
    assert data["priority"] == 3
    assert data["id"] > 0


def test_create_task_in_progress_status(client):
    r = client.post(
        "/tasks", json={"title": "wip task", "priority": 2, "status": "in-progress"}, headers=AUTH
    )
    assert r.status_code == 201
    assert r.json()["status"] == "in-progress"


def test_create_task_empty_title_returns_422(client):
    r = client.post("/tasks", json={"title": "", "priority": 3}, headers=AUTH)
    assert r.status_code == 422


def test_create_task_title_too_long_returns_422(client):
    r = client.post("/tasks", json={"title": "x" * 121, "priority": 3}, headers=AUTH)
    assert r.status_code == 422


def test_create_task_priority_too_low_returns_422(client):
    r = client.post("/tasks", json={"title": "Task", "priority": 0}, headers=AUTH)
    assert r.status_code == 422


def test_create_task_priority_too_high_returns_422(client):
    r = client.post("/tasks", json={"title": "Task", "priority": 6}, headers=AUTH)
    assert r.status_code == 422


def test_create_task_invalid_status_returns_400(client):
    r = client.post("/tasks", json={"title": "Task", "priority": 3, "status": "bad"}, headers=AUTH)
    assert r.status_code == 400


# ── Read ──────────────────────────────────────────────────────────────────────


def test_get_task_not_found_returns_404(client):
    r = client.get("/tasks/999999", headers=AUTH)
    assert r.status_code == 404


def test_get_task_returns_task(client):
    task = _create(client, "Read Me Task", 4)
    r = client.get(f"/tasks/{task['id']}", headers=AUTH)
    assert r.status_code == 200
    assert r.json()["title"] == "Read Me Task"
    assert r.json()["priority"] == 4


def test_get_tasks_returns_list(client):
    _create(client, "List A", 1)
    _create(client, "List B", 2)
    r = client.get("/tasks", headers=AUTH)
    assert r.status_code == 200
    assert isinstance(r.json(), list)
    assert len(r.json()) >= 2


# ── Update ────────────────────────────────────────────────────────────────────


def test_update_task_partial(client):
    task = _create(client, "Patch Me Task", 3, "todo")
    r = client.patch(f"/tasks/{task['id']}", json={"status": "in-progress"}, headers=AUTH)
    assert r.status_code == 200
    updated = r.json()
    assert updated["title"] == "Patch Me Task"  # unchanged
    assert updated["status"] == "in-progress"  # updated
    assert updated["priority"] == 3  # unchanged


def test_update_task_not_found_returns_404(client):
    r = client.patch("/tasks/999999", json={"title": "X"}, headers=AUTH)
    assert r.status_code == 404


# ── Delete ────────────────────────────────────────────────────────────────────


def test_delete_task(client):
    task = _create(client, "Delete Me Task", 1)
    r = client.delete(f"/tasks/{task['id']}", headers=AUTH)
    assert r.status_code == 200
    assert r.json()["deleted"] is True

    gone = client.get(f"/tasks/{task['id']}", headers=AUTH)
    assert gone.status_code == 404


# ── Filters ───────────────────────────────────────────────────────────────────


def test_filter_by_status(client):
    _create(client, "py-filter-todo-unique", 3, "todo")
    _create(client, "py-filter-done-unique", 3, "completed")

    r = client.get("/tasks?status=completed", headers=AUTH)
    tasks = r.json()
    assert all(t["status"] == "completed" for t in tasks)


def test_filter_by_due_date(client):
    target = "2099-11-11"
    _create(client, "py-date-match-unique", 3, "todo", target)
    _create(client, "py-date-miss-unique", 3, "todo", "2099-11-10")

    r = client.get(f"/tasks?due_date={target}", headers=AUTH)
    titles = [t["title"] for t in r.json()]
    assert "py-date-match-unique" in titles
    assert "py-date-miss-unique" not in titles
