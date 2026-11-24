#!/usr/bin/env bash
# Pre-PR gate: build, tests, Terraform static checks, forbidden files in the diff.
# Run from the repository root. Exit code 1 if any check fails.
set -u

fail=0
step() { printf '\n== %s\n' "$1"; }

changed="$( { git diff --name-only origin/main...HEAD 2>/dev/null; git diff --name-only; git diff --cached --name-only; } | sort -u)"

step "build (warnings are errors)"
dotnet restore > /dev/null && dotnet build -c Release --no-restore -v q || fail=1

step "tests"
dotnet test -c Release --no-build -v q || fail=1

if printf '%s\n' "$changed" | grep -q '^infra/'; then
  step "terraform fmt + validate"
  terraform -chdir=infra fmt -check -recursive || { echo "fmt: FAILED"; fail=1; }
  if terraform -chdir=infra init -backend=false -input=false > /dev/null; then
    terraform -chdir=infra validate || fail=1
  else
    echo "init FAILED, so validate was NOT run (is registry.terraform.io reachable?)"
    fail=1
  fi
else
  step "terraform (skipped: infra/ unchanged)"
fi

step "forbidden files in the diff"
forbidden='(^|/)(local\.settings\.json|terraform\.tfvars|backend\.tf|crash\.log)$|\.tfstate(\.|$)|(^|/)\.terraform/|\.session$|(^|/)\.env($|\.)'
hits="$(printf '%s\n' "$changed" | grep -E "$forbidden" | grep -v '\.example$' || true)"
if [ -n "$hits" ]; then
  echo "Forbidden files changed:"; echo "$hits"; fail=1
else
  echo "none"
fi

step "result"
if [ "$fail" -eq 0 ]; then echo "PASSED"; else echo "FAILED"; fi
exit "$fail"
