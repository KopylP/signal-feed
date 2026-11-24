#!/usr/bin/env bash
# Prints a random word for a new changelog section: lowercase letters, not used by any section or release tag yet.
# Run from the repository root: bash .claude/skills/write-changelog/scripts/new-word.sh
set -eu

words="albatross amber anchor aspen aurora badger basalt beacon birch bison bramble breeze cairn canyon cedar cinder clover cobalt comet condor coral cosmos coyote crane crimson cypress dahlia dolphin dune eagle ember falcon fennel fern fjord flint fox gecko glacier granite harbor hazel heron hickory horizon indigo iris jasper juniper kestrel lagoon lantern lark lemur lichen lilac lotus lynx magnolia maple marble meadow meteor mint monsoon moss nebula nectar nimbus oasis obsidian olive onyx opal orchid osprey otter panther pebble pelican pepper pine plover prairie quartz quokka raven reef ridge river robin saffron sage sequoia sierra sparrow spruce summit tamarin thistle thunder tundra velvet violet walnut willow wren yarrow zephyr"

used="$( { grep -oE '^## \[[^]]+\]' CHANGELOG.md 2>/dev/null || true; git tag 2>/dev/null || true; } | tr '[:upper:]' '[:lower:]' | grep -oE '[a-z]{3,}' || true)"

free=""
for w in $words; do
  printf '%s\n' "$used" | grep -qx "$w" || free="$free $w"
done
[ -n "$free" ] || { echo "No free word left: add more words to this script" >&2; exit 1; }
printf '%s\n' $free | shuf -n 1
