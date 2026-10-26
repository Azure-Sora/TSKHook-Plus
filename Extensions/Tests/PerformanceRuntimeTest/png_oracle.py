"""Independent pixel oracle for the real image package; requires Pillow."""
import hashlib
import json
import pathlib
import sys
from PIL import Image

root = pathlib.Path(sys.argv[1])
result = {}
for file in root.glob("*.png"):
    with Image.open(file) as image:
        rgba = image.convert("RGBA").transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes()
        result[file.name] = hashlib.sha256(rgba).hexdigest()
pathlib.Path(sys.argv[2]).write_text(json.dumps(result, indent=2), encoding="utf-8")
print(f"Independent Pillow oracle: {len(result)} PNGs")
