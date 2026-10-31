#!/usr/bin/env python3
"""Verify the exact-game note-color setter path used by the optional bridge."""

import argparse
import hashlib
import struct
from pathlib import Path

import pefile


GAME_HASH = "12bc21e5a88a2fc9abc259d5d67f24835145eabca6c3f863b075ba37ad75f197"
SETTER_RVA = 0x2884F00
ICALL_CACHE_RVA = 0x3F1E840
SETTER_PROLOGUE = bytes.fromhex("48895c2408574883ec20488b052f996901")
NOTE_COLOR_CALLS = (0x353431, 0x3534E9, 0x3535A1, 0x353659)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game_assembly", type=Path)
    path = parser.parse_args().game_assembly
    data = path.read_bytes()
    assert hashlib.sha256(data).hexdigest() == GAME_HASH, "GameAssembly hash differs"
    image = pefile.PE(data=data, fast_load=True)

    def at(rva: int, size: int) -> bytes:
        return data[image.get_offset_from_rva(rva):][:size]

    assert at(SETTER_RVA, len(SETTER_PROLOGUE)) == SETTER_PROLOGUE
    displacement = struct.unpack("<i", SETTER_PROLOGUE[-4:])[0]
    assert SETTER_RVA + len(SETTER_PROLOGUE) + displacement == ICALL_CACHE_RVA
    for site in NOTE_COLOR_CALLS:
        instruction = at(site, 5)
        assert instruction[0] == 0xE8, f"{site:#x} is no longer a direct call"
        assert site + 5 + struct.unpack("<i", instruction[1:])[0] == SETTER_RVA
    print("Verified sprite-color setter, Unity internal-call cache, and four note-color callsites.")


if __name__ == "__main__":
    main()
