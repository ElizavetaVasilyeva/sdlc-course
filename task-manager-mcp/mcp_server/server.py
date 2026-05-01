import json
from datetime import date

from mcp.server.fastmcp import FastMCP

from mcp_server import client

mcp = FastMCP("Task Manager")


# ── Tools ─────────────────────────────────────────────────────────────────────


@mcp.tool()
async def get_task(task_id: int) -> dict:
    """Return a single task by its ID."""
    return await client.get_task(task_id)


@mcp.tool()
async def get_all_tasks(
    status: str | None = None,
    due_date: str | None = None,
) -> list:
    """Return all tasks, optionally filtered by status and/or due_date (YYYY-MM-DD)."""
    return await client.get_all_tasks(status=status, due_date=due_date)


@mcp.tool()
async def add_task(
    title: str,
    priority: int,
    description: str | None = None,
    due_date: str | None = None,
    status: str = "todo",
) -> dict:
    """Create a new task. priority must be 1–5 (5 = highest).
    status: todo | in-progress | completed."""
    return await client.add_task(
        title=title,
        priority=priority,
        description=description,
        due_date=due_date,
        status=status,
    )


@mcp.tool()
async def update_task(
    task_id: int,
    title: str | None = None,
    description: str | None = None,
    priority: int | None = None,
    due_date: str | None = None,
    status: str | None = None,
) -> dict:
    """Update one or more fields of an existing task. Only supplied fields are changed."""
    return await client.update_task(
        task_id=task_id,
        title=title,
        description=description,
        priority=priority,
        due_date=due_date,
        status=status,
    )


@mcp.tool()
async def delete_task(task_id: int) -> dict:
    """Permanently delete a task by its ID."""
    return await client.delete_task(task_id)


# ── Resources ─────────────────────────────────────────────────────────────────


@mcp.resource("tasks://all")
async def resource_all_tasks() -> str:
    """All tasks in the system."""
    tasks = await client.get_all_tasks()
    return json.dumps(tasks, indent=2)


@mcp.resource("tasks://completed")
async def resource_completed_tasks() -> str:
    """Tasks with status = completed."""
    tasks = await client.get_all_tasks(status="completed")
    return json.dumps(tasks, indent=2)


@mcp.resource("tasks://today")
async def resource_today_tasks() -> str:
    """Tasks due today."""
    today = date.today().isoformat()
    tasks = await client.get_all_tasks(due_date=today)
    return json.dumps(tasks, indent=2)


@mcp.resource("tasks://in-progress")
async def resource_in_progress_tasks() -> str:
    """Tasks with status = in-progress."""
    tasks = await client.get_all_tasks(status="in-progress")
    return json.dumps(tasks, indent=2)


# ── Prompts ───────────────────────────────────────────────────────────────────


@mcp.prompt()
async def daily_plan() -> str:
    """Generate a focused daily plan from today's tasks."""
    today = date.today().isoformat()
    tasks = await client.get_all_tasks(due_date=today)
    tasks_json = json.dumps(tasks, indent=2)
    return (
        f"Here are today's tasks (due date: {today}):\n\n{tasks_json}\n\n"
        "Please review these tasks and return the top 3 highest-priority tasks "
        "I should focus on today. For each task include: its title, priority, "
        "status, and a brief reason why it should be done first. "
        "Order them by priority (highest first) and due date."
    )


@mcp.prompt()
async def prioritize_tasks() -> str:
    """Suggest a priority order for all open tasks."""
    tasks = await client.get_all_tasks(status="todo")
    in_progress = await client.get_all_tasks(status="in-progress")
    all_open = tasks + in_progress
    tasks_json = json.dumps(all_open, indent=2)
    return (
        f"Here are all open tasks:\n\n{tasks_json}\n\n"
        "Please suggest an ordered priority list for these tasks. "
        "Consider due date (earlier = more urgent), priority score (1–5, "
        "where 5 is highest), and status (in-progress tasks should generally "
        "be completed before starting new ones). "
        "Return a numbered list with the task title, current priority, due date, "
        "status, and a short justification for its position."
    )


# ── Entry point ───────────────────────────────────────────────────────────────

if __name__ == "__main__":
    mcp.run()
