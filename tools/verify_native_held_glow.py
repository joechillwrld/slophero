#!/usr/bin/env python3
"""Verify the exact-build held-sustain glow filter callsites.

Usage: PYTHONPATH=/path/to/pefile-capstone python tools/verify_native_held_glow.py GameAssembly.dll
"""

import argparse
import hashlib
import re
import struct
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64


GAME_SHA256 = "12bc21e5a88a2fc9abc259d5d67f24835145eabca6c3f863b075ba37ad75f197"
SETTER_RVA = 0x2870FC0
CALLS = {
    0x11559DB: "E8E0B57101",
    0x1156127: "E894AE7101",
    0x115615F: "E85CAE7101",
    0x11563BC: "E8FFAB7101",
    0x1156987: "E834A67101",
    0x11569BF: "E8FCA57101",
    0x1156C33: "E888A37101",
}
HELPERS = ((0x1155460, 0x1156710), (0x1156710, 0x1157010))


def verify(path: Path) -> None:
    data = path.read_bytes()
    assert hashlib.sha256(data).hexdigest() == GAME_SHA256, "GameAssembly hash differs"
    image = pefile.PE(data=data, fast_load=True)
    decoder = Cs(CS_ARCH_X86, CS_MODE_64)
    seen = set()
    for start, end in HELPERS:
        method = data[image.get_offset_from_rva(start):image.get_offset_from_rva(end)]
        for instruction in decoder.disasm(method, start):
            if instruction.mnemonic == "call" and instruction.op_str == f"{SETTER_RVA:#x}":
                seen.add(instruction.address)
    assert seen == set(CALLS), f"unmapped Renderer.enabled callsites: {seen ^ set(CALLS)}"

    source = (Path(__file__).parents[1] / "src/SlopHero.Mod/NativeHeldSustainGlowSuppressor.cs").read_text()
    match = re.search(r"private const int RendererEnabledSetterRva = (0x[0-9A-Fa-f]+);", source)
    assert match and int(match.group(1), 16) == SETTER_RVA, "C# setter RVA differs"
    for rva, expected in CALLS.items():
        actual = data[image.get_offset_from_rva(rva):][:5]
        assert actual.hex().upper() == expected, f"changed callsite {rva:#x}"
        target = rva + 5 + struct.unpack("<i", actual[1:])[0]
        assert target == SETTER_RVA, f"wrong call target at {rva:#x}"
        assert re.search(rf"new\(0x{rva:X},\s*\"{expected}\"\)", source), f"C# callsite {rva:#x} differs"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("game_assembly", type=Path)
    args = parser.parse_args()
    verify(args.game_assembly)
    print("Held-sustain glow filter matches the seven exact-build Renderer.enabled calls.")


if __name__ == "__main__":
    main()
