#!/bin/bash


RESOLUTION="1366,768"  # width,height
OUTPUT_DIR="./webui_screenshots"
GIF_OUTPUT="webui_screenshots/output.gif"
DELAY=200  # delay between frames in GIF (ms)


# -----------------------------
# Convert to GIF using ImageMagick
# -----------------------------
# The /visualise/ screenshots are captured at a much taller viewport so the
# full diagram fits in one frame — those are too tall to roll into the
# uniform-size rotating GIF, so we leave them out (they live as standalone
# images in webui_screenshots/ for embedding directly in the README/docs).
echo "Creating GIF..."
convert -delay $DELAY -loop 0 \
  $(ls "$OUTPUT_DIR"/*.png | grep -v "_visualise_") \
  "$GIF_OUTPUT"

echo "Done! GIF saved to $GIF_OUTPUT"
