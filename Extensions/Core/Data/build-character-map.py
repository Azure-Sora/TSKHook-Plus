"""Rebuild the compact table from pinned, checksum-verified OpenCC character data."""
from pathlib import Path
import gzip
import hashlib
import struct
import urllib.request

COMMIT = "8cf737a4e193db27d675b2ce9867d84aace33745"
SOURCES = [
    ("TSCharacters.txt", "9ff46a7d30e5765375eb13d33f2b03a34d298913caf2b120380679f33ae1642d"),
    ("STCharacters.txt", "a0ca1601c70648cf48b33c3c6210ccbecc5c7eead4b4c3daf76587ba2c03582b"),
]
blocks = []
for name, expected in SOURCES:
    url = f"https://raw.githubusercontent.com/BYVoid/OpenCC/{COMMIT}/data/dictionary/{name}"
    raw = urllib.request.urlopen(url, timeout=30).read()
    if hashlib.sha256(raw).hexdigest() != expected:
        raise ValueError("OpenCC source checksum changed: " + name)
    pairs = {}
    for line in raw.decode("utf-8").splitlines():
        if not line or line.startswith("#"):
            continue
        source, values = line.split("\t")
        target = values.split(" ")[0]
        if len(source) == len(target) == 1 and 0x4E00 <= ord(source) <= 0x9FFF and 0x4E00 <= ord(target) <= 0x9FFF and source != target:
            pairs[ord(source)] = ord(target)
    sorted_pairs = sorted(pairs.items())
    sources = [s for s, _ in sorted_pairs]
    deltas = [sources[0]] + [sources[i] - sources[i - 1] for i in range(1, len(sources))]
    targets = [t for _, t in sorted_pairs]
    blocks.append(struct.pack("<i", len(sources)) + struct.pack("<" + "H" * len(sources), *deltas) + struct.pack("<" + "H" * len(sources), *targets))
root = Path(__file__).resolve().parent
(root / "chinese-characters.gz").write_bytes(gzip.compress(b"".join(blocks), compresslevel=9, mtime=0))
(root / "OpenCC-LICENSE.txt.gz").write_bytes(gzip.compress((root / "OpenCC-LICENSE.txt").read_bytes(), compresslevel=9, mtime=0))
print("Rebuilt", root / "chinese-characters.gz")
