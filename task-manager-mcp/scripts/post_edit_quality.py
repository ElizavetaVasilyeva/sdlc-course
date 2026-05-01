"""
Post-edit hook: run ruff, black --check, and pytest after file edits.
Reports pass/fail; does not auto-fix. Claude Code will see the output
and can decide to fix issues explicitly.
"""

import json
import subprocess
import sys
from pathlib import Path

UV = r"C:\Users\l.zahorskaya\.local\bin\uv.exe"
PROJECT_ROOT = Path(__file__).parent.parent


def run(cmd: list[str]) -> tuple[int, str]:
    result = subprocess.run(
        cmd,
        cwd=PROJECT_ROOT,
        capture_output=True,
        text=True,
    )
    output = (result.stdout + result.stderr).strip()
    return result.returncode, output


def main() -> None:
    try:
        raw = sys.stdin.read()
        data = json.loads(raw) if raw.strip() else {}
        tool_input = data.get("tool_input", {})
        file_path = tool_input.get("file_path", "")

        ext = Path(file_path).suffix if file_path else ""
        is_python = ext == ".py" or not file_path

        results: list[str] = []
        all_passed = True

        code, out = run([UV, "run", "ruff", "check", "."])
        status = "PASS" if code == 0 else "FAIL"
        if code != 0:
            all_passed = False
        results.append(f"ruff:  {status}")
        if code != 0 and out:
            results.append(out[:500])

        code, out = run([UV, "run", "black", "--check", "."])
        status = "PASS" if code == 0 else "FAIL"
        if code != 0:
            all_passed = False
        results.append(f"black: {status}")
        if code != 0 and out:
            results.append(out[:300])

        if is_python:
            code, out = run([UV, "run", "pytest", "tests/test_mcp_contract.py", "-q", "--tb=short"])
            status = "PASS" if code == 0 else "FAIL"
            if code != 0:
                all_passed = False
            results.append(f"pytest (contract): {status}")
            if code != 0 and out:
                results.append(out[:500])

        summary = (
            "All quality checks passed." if all_passed else "Quality checks FAILED — see above."
        )
        print("\n".join(results))
        print(summary)

    except Exception as exc:  # noqa: BLE001
        print(f"post_edit_quality.py error: {exc}")
        sys.exit(1)


if __name__ == "__main__":
    main()
