# SPDX-License-Identifier: AGPL-3.0-only
import sys
from pathlib import Path

def main():
    source, backend, cache = map(Path, sys.argv[1:4])
    sys.path.insert(0, str(backend))
    from . import legacy, structured
    legacy.ROOT = cache
    legacy.DEST = cache/'Converted'
    legacy.DEST.mkdir(parents=True, exist_ok=True)
    legacy.process(source, experimental="--experimental" in sys.argv[4:])

if __name__ == '__main__':
    main()
