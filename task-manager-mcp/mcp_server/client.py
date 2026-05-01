import os
from typing import Any

import httpx

_BASE_URL = os.getenv("TASK_API_BASE_URL", "http://127.0.0.1:8000")
_API_KEY = os.getenv("TASK_API_KEY", "")
_HEADERS = {"X-API-Key": _API_KEY}


def _client() -> httpx.AsyncClient:
    return httpx.AsyncClient(base_url=_BASE_URL, headers=_HEADERS, timeout=10.0)


async def _check(response: httpx.Response) -> dict[str, Any]:
    if not response.is_success:
        raise RuntimeError(f"API error {response.status_code}: {response.text}")
    return response.json()


async def get_task(task_id: int) -> dict[str, Any]:
    async with _client() as c:
        return await _check(await c.get(f"/tasks/{task_id}"))


async def get_all_tasks(
    status: str | None = None,
    due_date: str | None = None,
) -> list[dict[str, Any]]:
    params: dict[str, str] = {}
    if status is not None:
        params["status"] = status
    if due_date is not None:
        params["due_date"] = due_date
    async with _client() as c:
        return await _check(await c.get("/tasks", params=params))  # type: ignore[return-value]


async def add_task(
    title: str,
    priority: int,
    description: str | None = None,
    due_date: str | None = None,
    status: str = "todo",
) -> dict[str, Any]:
    payload = {
        "title": title,
        "priority": priority,
        "description": description,
        "due_date": due_date,
        "status": status,
    }
    async with _client() as c:
        return await _check(await c.post("/tasks", json=payload))


async def update_task(
    task_id: int,
    title: str | None = None,
    description: str | None = None,
    priority: int | None = None,
    due_date: str | None = None,
    status: str | None = None,
) -> dict[str, Any]:
    payload = {
        k: v
        for k, v in {
            "title": title,
            "description": description,
            "priority": priority,
            "due_date": due_date,
            "status": status,
        }.items()
        if v is not None
    }
    async with _client() as c:
        return await _check(await c.patch(f"/tasks/{task_id}", json=payload))


async def delete_task(task_id: int) -> dict[str, Any]:
    async with _client() as c:
        return await _check(await c.delete(f"/tasks/{task_id}"))
