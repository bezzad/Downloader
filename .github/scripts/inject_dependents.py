#!/usr/bin/env python3
"""Inject the top-N dependent repositories into README.md.

The `nvuillam/github-dependents-info` action writes its full report (a table of
dependent repositories with avatars and star counts) to a standalone markdown file.
This script extracts the highest-starred repository rows from that report and writes
them, with their avatars, into README.md between the markers:

    <!-- gh-dependents-info-list-start -->
    <!-- gh-dependents-info-list-end -->

so the README shows a compact, icon-rich "top dependents" list (similar to the
Contributors section) that stays in sync with the generated report.
"""
import argparse
import re
import sys

START = "<!-- gh-dependents-info-list-start -->"
END = "<!-- gh-dependents-info-list-end -->"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, help="Generated dependents report markdown file.")
    parser.add_argument("--readme", required=True, help="README file to inject the list into.")
    parser.add_argument("--top", type=int, default=20, help="Maximum number of repositories to show.")
    args = parser.parse_args()

    with open(args.source, encoding="utf-8") as handle:
        report = handle.read()

    # Repository rows are the avatar lines emitted by the tool, e.g.:
    #   |<img ... src="https://avatars..."> &nbsp; [owner](url) / [repo](url) | 12345 |
    # The summary/package tables use badge links instead, so filtering on the leading
    # "|<img" reliably selects only the per-repository rows (already sorted by stars).
    rows = [line for line in report.splitlines() if line.startswith("|<img")]
    if not rows:
        print("No dependent repository rows found in report; leaving README unchanged.")
        return 0

    top_rows = rows[: args.top]
    table = "\n".join(["| Repository | Stars |", "| :-- | --: |", *top_rows])
    block = f"{START}\n{table}\n{END}"

    with open(args.readme, encoding="utf-8") as handle:
        readme = handle.read()

    pattern = re.compile(re.escape(START) + r".*?" + re.escape(END), re.DOTALL)
    if not pattern.search(readme):
        print(f"Markers not found in {args.readme}; nothing to inject.", file=sys.stderr)
        return 1

    updated = pattern.sub(lambda _: block, readme)
    if updated == readme:
        print("Dependent repositories list already up to date.")
        return 0

    with open(args.readme, "w", encoding="utf-8") as handle:
        handle.write(updated)

    print(f"Injected {len(top_rows)} dependent repositories into {args.readme}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
