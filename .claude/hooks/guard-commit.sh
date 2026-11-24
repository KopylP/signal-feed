#!/usr/bin/env bash
# PreToolUse hook for Bash: block `git commit` while files that must never be committed are staged.
# .gitignore already keeps them out; this catches `git add -f`. Exit code 2 blocks the call and shows stderr to Claude.
set -u

input="$(cat)"
command="$(printf '%s' "$input" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("tool_input",{}).get("command",""))' 2>/dev/null || true)"

case "$command" in
  *"git commit"*) ;;
  *) exit 0 ;;
esac

forbidden='(^|/)(local\.settings\.json|terraform\.tfvars|backend\.tf|crash\.log)$|\.tfstate(\.|$)|(^|/)\.terraform/'
staged="$(git diff --cached --name-only 2>/dev/null | grep -E "$forbidden" || true)"

if [ -n "$staged" ]; then
  {
    echo "Blocked: these files must never be committed (secrets, state, local settings):"
    echo "$staged"
    echo "Unstage them with: git restore --staged <file>"
  } >&2
  exit 2
fi
exit 0
