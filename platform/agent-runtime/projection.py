#!/usr/bin/env python3
"""Validate/regenerate a reproducible, self-contained projection of the shared agent runtime.

Canonical implementation lives in the Ecosystem AI agent runtime. Lunet includes
the identical source snapshot in its own application for independent builds.
Never manually change generated files; fix the canonical source then --sync.
"""
from pathlib import Path
import argparse
import sys

ROOT = Path(__file__).resolve().parents[2]
CANONICAL = ROOT / "apps" / "ecosystem-ai" / "src"
GENERATED = ROOT / "apps" / "lunet2d" / "src" / "Lunet.Ai" / "Generated"


def pairs():
    core = CANONICAL / "AgentRuntime.Core"
    for source in sorted(core.glob("*.cs")):
        yield source, GENERATED / "Core" / source.name
    source = CANONICAL / "AgentRuntime.Providers.ChatCompletions" / "ChatCompletionsProvider.cs"
    yield source, GENERATED / "Providers" / source.name


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--sync", action="store_true", help="regenerate Lunet's canonical runtime snapshot")
    args = parser.parse_args()
    mismatches = []
    expected = set()
    for source, generated in pairs():
        expected.add(generated)
        if args.sync:
            generated.parent.mkdir(parents=True, exist_ok=True)
            generated.write_bytes(source.read_bytes())
        elif not generated.exists() or generated.read_bytes() != source.read_bytes():
            mismatches.append(str(generated.relative_to(ROOT)))
    for directory in [GENERATED / "Core", GENERATED / "Providers"]:
        if directory.is_dir():
            for generated in directory.glob("*.cs"):
                if generated not in expected:
                    mismatches.append(str(generated.relative_to(ROOT)) + " (obsolete)")
                    if args.sync:
                        generated.unlink()
    if mismatches and not args.sync:
        print("ERROR: agent runtime projection diverged:")
        print("\n".join("  " + item for item in mismatches))
        print("Run python3 platform/agent-runtime/projection.py --sync and commit regenerated files.")
        return 1
    print("Agent runtime projection is synchronized.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
