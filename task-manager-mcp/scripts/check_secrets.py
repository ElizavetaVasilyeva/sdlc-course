"""
Pre-edit hook: block writes that contain likely real secrets.
Claude Code passes hook context as JSON on stdin.
Exit 0 always; communicate block via JSON decision on stdout.
"""

import json
import re
import sys

PLACEHOLDER_VALUES = {
    "dev-task-manager-key-change-me",
    "your-api-key-here",
    "change-me",
    "placeholder",
    "example",
}

SECRET_PATTERNS = [
    re.compile(
        r'TASK_API_KEY\s*=\s*["\']?(?!dev-task-manager-key-change-me)([A-Za-z0-9_\-]{16,})["\']?'
    ),
    re.compile(
        r'(?i)(api[_-]?key|secret|password|token|bearer)\s*[=:]\s*["\']?([A-Za-z0-9_\-]{16,})["\']?'
    ),
    re.compile(r"(?i)-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    re.compile(r"(?i)aws_secret_access_key\s*=\s*\S+"),
    re.compile(r"(?i)AKIA[0-9A-Z]{16}"),
]


def _is_placeholder(value: str) -> bool:
    return any(p in value.lower() for p in PLACEHOLDER_VALUES)


def _scan(content: str) -> str | None:
    for pattern in SECRET_PATTERNS:
        match = pattern.search(content)
        if match:
            matched = match.group(0)
            if not _is_placeholder(matched):
                return matched
    return None


def main() -> None:
    try:
        raw = sys.stdin.read()
        if not raw.strip():
            print(json.dumps({}))
            return

        data = json.loads(raw)
        tool_input = data.get("tool_input", {})

        content_to_check = ""
        for field in ("content", "new_string"):
            if field in tool_input:
                content_to_check += tool_input[field] + "\n"

        if not content_to_check.strip():
            print(json.dumps({}))
            return

        hit = _scan(content_to_check)
        if hit:
            # Truncate match for log safety
            safe_hit = hit[:60] + "..." if len(hit) > 60 else hit
            print(
                json.dumps(
                    {
                        "decision": "block",
                        "reason": f"Likely secret detected in content: {safe_hit!r}. "
                        "Store secrets in .env and never commit them.",
                    }
                )
            )
        else:
            print(json.dumps({}))

    except Exception as exc:  # noqa: BLE001
        print(json.dumps({"decision": "block", "reason": f"check_secrets.py error: {exc}"}))
        sys.exit(1)


if __name__ == "__main__":
    main()
