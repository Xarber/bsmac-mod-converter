# SPDX-License-Identifier: AGPL-3.0-only
import sys
from pathlib import Path

def main():
    source, backend, cache = map(Path, sys.argv[1:])
    sys.path.insert(0, str(backend))
    from . import legacy, structured
    legacy.ROOT = cache
    legacy.DEST = cache/'Converted'
    legacy.DEST.mkdir(parents=True, exist_ok=True)
    legacy.process(source)

if __name__ == '__main__':
    main()
