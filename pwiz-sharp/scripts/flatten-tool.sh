#!/bin/bash
# Flatten Tools/<...>/<src-or-test>/<X>/* up to Tools/<...>/<src-or-test>/*
# Usage: flatten-tool.sh <inner-dir>
# e.g.   flatten-tool.sh Tools/SeeMS/src/SeeMS
# After running: inner dir is gone, files are at parent level, bin/obj wiped.
set -e

INNER="$1"
if [ -z "$INNER" ] || [ ! -d "$INNER" ]; then
    echo "usage: $0 <inner-dir>" >&2
    exit 2
fi

cd "$INNER"
for entry in *; do
    # Skip bin/obj — those are build output and will be deleted next.
    if [ "$entry" = "bin" ] || [ "$entry" = "obj" ]; then continue; fi
    git mv "$entry" "../$entry"
done
cd ..
INNER_NAME="$(basename "$INNER")"
rm -rf "$INNER_NAME/bin" "$INNER_NAME/obj"
rmdir "$INNER_NAME"
echo "Flattened: $INNER -> $(pwd)"
