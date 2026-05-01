---
description: Generate a concise conventional commit message from the current git diff.
---

You are generating a git commit message for this project.

Steps:
1. Run `git diff --staged` to see what is staged. If nothing is staged, run `git diff HEAD` to see all unstaged changes.
2. Summarize the behaviour changes — not the file names, the actual effect on the system.
3. Choose exactly one type: `feat`, `fix`, `test`, `docs`, `refactor`, or `chore`.
4. Write a commit title in the format `type(scope): short summary` (max 72 characters). Scope is optional but use `api`, `mcp`, `tests`, or `hooks` when relevant.
5. If the change is non-trivial, add a blank line followed by a short body (2–4 sentences max) explaining the why.
6. Output only the commit message — no extra commentary, no code blocks, no explanations.

Constraints:
- Never include secrets, API keys, or credentials in the commit message.
- Never reference ticket numbers or issue IDs unless they are already in the diff.
- Do not describe unrelated changes that are not in the diff.
