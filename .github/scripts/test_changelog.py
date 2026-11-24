"""Tests for changelog.py. Run: python3 -m unittest discover -s .github/scripts"""
import contextlib
import io
import os
import subprocess
import tempfile
import unittest

import changelog

SECTION = "## [{w}] - 2026-11-24\n\n### Added\n- **A feature**: it does a thing.\n"
HEADER = "# Changelog\n\nIntro.\n\n"


def read(path):
    with open(path) as f:
        return f.read()


def run(cwd, *args):
    return subprocess.run(args, cwd=cwd, check=True, capture_output=True, text=True).stdout.strip()


class ChangelogTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.cwd = os.getcwd()
        os.chdir(self.dir.name)
        self.addCleanup(os.chdir, self.cwd)
        run(".", "git", "init", "-q")
        run(".", "git", "config", "user.email", "t@example.com")
        run(".", "git", "config", "user.name", "t")

    def commit(self, text):
        with open("CHANGELOG.md", "w") as f:
            f.write(text)
        run(".", "git", "add", "-A")
        run(".", "git", "commit", "-q", "-m", "c")
        return run(".", "git", "rev-parse", "HEAD")

    def test_word_added_by_the_push(self):
        before = self.commit(HEADER)
        after = self.commit(HEADER + SECTION.format(w="falcon"))
        self.assertEqual(changelog.word_added_by_push(before, after), "falcon")

    def test_no_word_when_the_push_adds_no_section(self):
        before = self.commit(HEADER + SECTION.format(w="falcon"))
        after = self.commit(HEADER + SECTION.format(w="falcon") + "\nmore\n")
        self.assertEqual(changelog.word_added_by_push(before, after), "")

    def test_first_push_has_no_word(self):
        sha = self.commit(HEADER + SECTION.format(w="falcon"))
        self.assertEqual(changelog.word_added_by_push("0" * 40, sha), "")

    def test_pending_words_are_listed_newest_first(self):
        text = HEADER + SECTION.format(w="otter") + "\n" + SECTION.format(w="falcon")
        sha = self.commit(text)
        self.assertEqual(changelog.pending_words(run(".", "git", "show", f"{sha}:CHANGELOG.md")), ["otter", "falcon"])

    def test_release_renames_only_the_named_words(self):
        text = HEADER + SECTION.format(w="otter") + "\n" + SECTION.format(w="falcon") + "\n## [v2026.11.20.5-heron] - 2026-11-20\n\n### Fixed\n- Old.\n"
        with open("CHANGELOG.md", "w") as f:
            f.write(text)
        changelog.release("CHANGELOG.md", "v2026.11.24.131", "falcon", "notes.md")
        out = read("CHANGELOG.md")
        self.assertIn("## [v2026.11.24.131-falcon] - 2026-11-24", out)
        self.assertIn("## [otter] - 2026-11-24", out)
        self.assertIn("## [v2026.11.20.5-heron]", out)
        notes = read("notes.md")
        self.assertIn("### Added", notes)
        self.assertNotIn("## [", notes)

    def test_release_with_several_words_labels_each_in_the_notes(self):
        text = HEADER + SECTION.format(w="otter") + "\n" + SECTION.format(w="falcon")
        with open("CHANGELOG.md", "w") as f:
            f.write(text)
        changelog.release("CHANGELOG.md", "v2026.11.24.131", "falcon,otter", "notes.md")
        notes = read("notes.md")
        self.assertIn("## falcon", notes)
        self.assertIn("## otter", notes)

    def test_release_is_idempotent(self):
        with open("CHANGELOG.md", "w") as f:
            f.write(HEADER + SECTION.format(w="falcon"))
        changelog.release("CHANGELOG.md", "v2026.11.24.131", "falcon", "notes.md")
        first = read("CHANGELOG.md")
        changelog.release("CHANGELOG.md", "v2026.11.24.131", "falcon", "notes.md")
        self.assertEqual(read("CHANGELOG.md"), first)
        self.assertEqual(read("notes.md"), "")

    def test_validate_accepts_a_good_file(self):
        with open("CHANGELOG.md", "w") as f:
            f.write(HEADER + SECTION.format(w="falcon"))
        self.assertEqual(changelog.validate("CHANGELOG.md"), 0)

    def test_validate_rejects_bad_headings_order_and_duplicates(self):
        bad_order = "## [falcon] - 2026-11-24\n\n### Fixed\n- x\n\n### Added\n- y\n"
        for text in (HEADER + "## [pending-3fa9c1] - 2026-11-24\n\n### Added\n- x\n",
                     HEADER + bad_order,
                     HEADER + SECTION.format(w="falcon") + "\n" + SECTION.format(w="falcon"),
                     HEADER + "## [falcon] - 2026-11-24\n\n### Misc\n- x\n"):
            with open("CHANGELOG.md", "w") as f:
                f.write(text)
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(changelog.validate("CHANGELOG.md"), 1, text)


if __name__ == "__main__":
    unittest.main()
