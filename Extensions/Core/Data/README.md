# Chinese character table

Derived from OpenCC (BYVoid and contributors), Apache-2.0. Only U+4E00..U+9FFF one-character mappings, first candidate, excluding identities. No phrases or regional vocabulary. GZip of two little-endian blocks: int32 count, uint16 delta-encoded sorted sources, then uint16 targets; TS first, ST second. Supplementary characters remain unchanged.

Upstream commit: `8cf737a4e193db27d675b2ce9867d84aace33745`

[
  {
    "file": "TSCharacters.txt",
    "url": "https://raw.githubusercontent.com/BYVoid/OpenCC/8cf737a4e193db27d675b2ce9867d84aace33745/data/dictionary/TSCharacters.txt",
    "sha256": "9ff46a7d30e5765375eb13d33f2b03a34d298913caf2b120380679f33ae1642d",
    "pairs": 2853
  },
  {
    "file": "STCharacters.txt",
    "url": "https://raw.githubusercontent.com/BYVoid/OpenCC/8cf737a4e193db27d675b2ce9867d84aace33745/data/dictionary/STCharacters.txt",
    "sha256": "a0ca1601c70648cf48b33c3c6210ccbecc5c7eead4b4c3daf76587ba2c03582b",
    "pairs": 2593
  }
]

Compressed bytes: 14520
