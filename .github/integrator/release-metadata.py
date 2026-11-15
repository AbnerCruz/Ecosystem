#!/usr/bin/env python3
"""Projection of maturity from the canonical version; never chooses a stage."""
import sys
from lifecycle import stage
command, version = sys.argv[1:]
s = stage(version)
print(s if command == 'stage' else ('--prerelease --latest=false' if s != 'stable' else '--latest'))
