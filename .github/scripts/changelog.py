#!/usr/bin/env python3
"""Changelog helper for the release pipeline and for the pre-PR check (standard library only).

A pull request adds a section headed `## [word] - YYYY-MM-DD` to CHANGELOG.md. The word is lowercase letters and stays
the name of the change. After a deploy, the release step puts the build version in front of it:
`## [v2026.11.24.131-falcon] - YYYY-MM-DD` (ADR-0007).

Commands:
  validate [FILE]             check the structure of the changelog (used by check.sh)
  word BEFORE SHA             print the word a push added, or nothing (build job)
  pending REF                 print every pending word at REF, newest first, comma separated (build job)
  release FILE VERSION WORDS NOTES
                              rename the pending sections named in WORDS and write the release notes to NOTES (release job)
"""
import re
import subprocess
import sys

PENDING = re.compile(r"^## \[([a-z]{3,20})\] - (\d{4}-\d{2}-\d{2})\s*$")
RELEASED = re.compile(r"^## \[(v\d{4}\.\d{2}\.\d{2}\.\d+(?:-[a-z]{3,20})?)\] - (\d{4}-\d{2}-\d{2})\s*$")
CATEGORIES = [
    "Setup required", "Contract changes", "Added", "Logic", "Improved",
    "Fixed", "Security", "Removed / Deprecated", "Dependencies",
]
ZEROS = "0" * 40


def git(*args):
    return subprocess.run(["git", *args], check=True, capture_output=True, text=True).stdout


def pending_words(text):
    return [m.group(1) for line in text.splitlines() if (m := PENDING.match(line))]


def sections(text):
    """Split into (heading line, body lines) pairs for every `## ` section."""
    result, current = [], None
    for line in text.splitlines():
        if line.startswith("## "):
            current = (line, [])
            result.append(current)
        elif current is not None:
            current[1].append(line)
    return result


def validate(path):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    errors, seen = [], set()
    for heading, body in sections(text):
        pending, released = PENDING.match(heading), RELEASED.match(heading)
        if not (pending or released):
            errors.append(f"bad section heading: {heading!r} (expected `## [word] - YYYY-MM-DD` or `## [vYYYY.MM.DD.N-word] - YYYY-MM-DD`)")
            continue
        name = (pending or released).group(1)
        if name in seen:
            errors.append(f"duplicate section: {name}")
        seen.add(name)
        found = [line[4:].strip() for line in body if line.startswith("### ")]
        for category in found:
            if category not in CATEGORIES:
                errors.append(f"{name}: unknown category {category!r}")
        known = [c for c in found if c in CATEGORIES]
        if known != sorted(known, key=CATEGORIES.index):
            errors.append(f"{name}: categories are out of order (expected: {', '.join(CATEGORIES)})")
        if len(set(found)) != len(found):
            errors.append(f"{name}: a category appears twice")
        if not found:
            errors.append(f"{name}: the section has no category")
    for error in errors:
        print(f"changelog: {error}")
    return 1 if errors else 0


def word_added_by_push(before, sha):
    if not before or before == ZEROS:
        return ""
    diff = git("diff", "-U0", before, sha, "--", "CHANGELOG.md")
    added = [m.group(1) for line in diff.splitlines() if line.startswith("+") and (m := PENDING.match(line[1:]))]
    return added[0] if added else ""


def release(path, version, words, notes_path):
    with open(path, encoding="utf-8") as f:
        lines = f.read().split("\n")
    wanted = [w for w in words.split(",") if w]
    notes, i = [], 0
    renamed = []
    while i < len(lines):
        m = PENDING.match(lines[i])
        if m and m.group(1) in wanted:
            word, date = m.groups()
            lines[i] = f"## [{version}-{word}] - {date}"
            j = i + 1
            while j < len(lines) and not lines[j].startswith("## "):
                j += 1
            body = "\n".join(lines[i + 1:j]).strip("\n")
            renamed.append((word, body))
            i = j
        else:
            i += 1
    for word, body in renamed:
        notes.append(body if len(renamed) == 1 else f"## {word}\n\n{body}")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    with open(notes_path, "w", encoding="utf-8") as f:
        f.write(("\n\n".join(notes) + "\n") if notes else "")
    return 0


def main(argv):
    cmd, args = (argv[1], argv[2:]) if len(argv) > 1 else ("", [])
    if cmd == "validate":
        return validate(args[0] if args else "CHANGELOG.md")
    if cmd == "word" and len(args) == 2:
        print(word_added_by_push(*args))
        return 0
    if cmd == "pending" and len(args) == 1:
        print(",".join(pending_words(git("show", f"{args[0]}:CHANGELOG.md"))))
        return 0
    if cmd == "release" and len(args) == 4:
        return release(*args)
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
