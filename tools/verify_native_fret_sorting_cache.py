#!/usr/bin/env python3
"""Verify current-game fret sorting callsites and emulate the cache bridge.

Usage: PYTHONPATH=/path/to/pefile-capstone-unicorn python \
    tools/verify_native_fret_sorting_cache.py GameAssembly.dll
"""

import argparse
import hashlib
import re
import struct
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64, UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_RCX, UC_X86_REG_RDX, UC_X86_REG_R8, UC_X86_REG_RSP


GAME_SHA256 = "12bc21e5a88a2fc9abc259d5d67f24835145eabca6c3f863b075ba37ad75f197"
SORTING_ORDER_SETTER = 0x28710E0
SITES = {
    0xA895F: ("E87C877C02", False),
    0xA897C: ("E85F877C02", False),
    0xA8999: ("E842877C02", False),
    0xA89B3: ("E828877C02", False),
    0xA89D0: ("E80B877C02", False),
    0xA89E9: ("E8F2867C02", False),
    0xA8A02: ("E8D9867C02", False),
    0xA9F31: ("E8AA717C02", False),
    0xA9F4E: ("E88D717C02", False),
    0xA9F67: ("E874717C02", False),
    0xA9F7D: ("E85E717C02", False),
    0xA9F96: ("E845717C02", False),
    0xA9FAF: ("E82C717C02", False),
    0xA9FCD: ("E90E717C02", True),
}


def bridge_bytes(source: str) -> bytearray:
    match = re.search(r"private const string CacheStubHex =\s*(.*?);", source, re.S)
    assert match, "bridge stub missing"
    return bytearray.fromhex("".join(re.findall(r'"([0-9A-F]+)"', match.group(1))))


def patch_marker(code: bytearray, marker: int, address: int) -> None:
    old = struct.pack("<Q", marker)
    assert code.count(old) == 1, f"marker {marker:#x} is missing or ambiguous"
    index = code.index(old)
    code[index:index + 8] = struct.pack("<Q", address)


def verify_game(path: Path) -> None:
    data = path.read_bytes()
    assert hashlib.sha256(data).hexdigest() == GAME_SHA256, "GameAssembly hash differs"
    image = pefile.PE(data=data, fast_load=True)
    decoder = Cs(CS_ARCH_X86, CS_MODE_64)
    for address, (expected, tail) in SITES.items():
        actual = data[image.get_offset_from_rva(address):][:5]
        assert actual.hex().upper() == expected, f"changed callsite {address:#x}"
        assert (actual[0] == 0xE9) == tail, f"wrong call/jump form at {address:#x}"
        target = address + 5 + struct.unpack("<i", actual[1:])[0]
        assert target == SORTING_ORDER_SETTER, f"wrong setter target at {address:#x}"

    expected_ranges = {
        (0xA88E0, 0xA8A07): {a for a, (_, tail) in SITES.items() if a < 0xA9000},
        (0xA9ED0, 0xA9FD2): {a for a in SITES if a >= 0xA9000},
    }
    for (start, end), expected in expected_ranges.items():
        method = data[image.get_offset_from_rva(start):image.get_offset_from_rva(end)]
        found = set()
        for instruction in decoder.disasm(method, start):
            if instruction.mnemonic not in ("call", "jmp"):
                continue
            if instruction.op_str == f"{SORTING_ORDER_SETTER:#x}":
                found.add(instruction.address)
        assert found == expected, f"unexpected setter paths in {start:#x}: {found ^ expected}"


def verify_bridge(source: str) -> None:
    code_addr, original_addr, stack_addr, end_addr = 0x10000000, 0x20000000, 0x30000000, 0x40000000
    allocation, table_addr = code_addr, code_addr + 0x1000
    skipped_addr, flag_addr = code_addr + 0x3000, code_addr + 0x3008
    code = bridge_bytes(source)
    patch_marker(code, 0x1122334455667788, flag_addr)
    patch_marker(code, 0x2233445566778899, table_addr)
    patch_marker(code, 0x1100FFEEDDCCBBAA, skipped_addr)
    patch_marker(code, 0x33445566778899AA, original_addr)

    vm = Uc(UC_ARCH_X86, UC_MODE_64)
    vm.mem_map(allocation, 0x4000)
    vm.mem_map(original_addr, 0x1000)
    vm.mem_map(stack_addr, 0x1000)
    vm.mem_map(end_addr, 0x1000)
    vm.mem_write(code_addr, bytes(code))
    vm.mem_write(original_addr, b"\xC3")
    setter_calls = []

    def observe(_vm, address, _size, _user):
        if address == original_addr:
            setter_calls.append((_vm.reg_read(UC_X86_REG_RCX), _vm.reg_read(UC_X86_REG_RDX)))

    vm.hook_add(UC_HOOK_CODE, observe)

    def invoke(renderer: int, order: int, active: bool = True) -> None:
        top = stack_addr + 0x800
        vm.mem_write(top, struct.pack("<Q", end_addr))
        vm.reg_write(UC_X86_REG_RSP, top)
        vm.reg_write(UC_X86_REG_RCX, renderer)
        vm.reg_write(UC_X86_REG_RDX, order & 0xFFFFFFFF)
        vm.reg_write(UC_X86_REG_R8, 0)
        vm.mem_write(flag_addr, b"\x01" if active else b"\x00")
        vm.emu_start(code_addr, end_addr)
        assert vm.reg_read(UC_X86_REG_RSP) == top + 8, "bad return-stack behavior"
        assert vm.reg_read(UC_X86_REG_RCX) == renderer, "renderer argument was clobbered"
        assert vm.reg_read(UC_X86_REG_RDX) == (order & 0xFFFFFFFF), "sorting order was clobbered"
        assert vm.reg_read(UC_X86_REG_R8) == 0, "method-info argument was clobbered"

    renderer = 0x50001000
    invoke(renderer, 20000)
    assert setter_calls == [(renderer, 20000)], "first write must pass through"
    invoke(renderer, 20000)
    assert len(setter_calls) == 1, "identical repeated write must skip"
    assert struct.unpack("<Q", vm.mem_read(skipped_addr, 8))[0] == 1
    invoke(renderer, 20001)
    assert setter_calls[-1] == (renderer, 20001), "changed order must pass through"
    invoke(renderer, 20001)
    assert len(setter_calls) == 2, "changed order should then cache"
    invoke(renderer, 20001, active=False)
    assert len(setter_calls) == 3, "inactive bridge must pass through"

    # Different renderers can collide in the direct map. That is a miss, never
    # a false hit: A, B, A all invoke the setter; the next B can be skipped.
    other = renderer + 0x2000  # same ((pointer >> 4) & 511) slot
    invoke(other, 20001)
    invoke(renderer, 20001)
    invoke(other, 20001)
    assert len(setter_calls) == 6, "collisions must fail open and then cache the current occupant"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("game_assembly", type=Path)
    args = parser.parse_args()
    verify_game(args.game_assembly)
    source = (Path(__file__).parents[1] / "src/SlopHero.Mod/NativeFretSortingOrderCache.cs").read_text()
    for address, (expected, tail) in SITES.items():
        assert re.search(rf"new\(0x{address:X},\s*\"{expected}\",\s*{str(tail).lower()}\)", source), (
            f"C# callsite {address:#x} differs"
        )
    verify_bridge(source)
    print("Fret sorting callsites and x64 cache bridge behavior verified.")


if __name__ == "__main__":
    main()
