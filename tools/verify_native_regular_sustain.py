#!/usr/bin/env python3
"""Verify exact sustain callsites and emulate all three x64 bridge stubs.

Usage: PYTHONPATH=/path/to/pefile-capstone-unicorn python tools/verify_native_regular_sustain.py GameAssembly.dll
"""

import argparse
import hashlib
import re
import struct
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64, UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_RCX, UC_X86_REG_RDX, UC_X86_REG_R8, UC_X86_REG_RSP, UC_X86_REG_XMM12


GAME_SHA256 = "12bc21e5a88a2fc9abc259d5d67f24835145eabca6c3f863b075ba37ad75f197"
SET_PROPERTY_BLOCK = 0x2870DE0
GET_PROPERTY_BLOCK = 0x2870D50
EARLY_CHECK = 0x1156B10
AFTER_PROPERTY_BLOCK = 0x1156B49
CALLS = {
    0x1156B44: "E897A27101",
    0x1155B9D: "E83EB27101",
    0x1155C52: "E889B17101",
    0x11562D0: "E80BAB7101",
    0x11565E7: "E8F4A77101",
    0x1156E74: "E8679F7101",
}


def stub(source: str, name: str) -> bytearray:
    match = re.search(rf"private const string {name} =\s*(.*?);", source, re.S)
    assert match, name
    return bytearray.fromhex("".join(re.findall(r'"([0-9A-F]+)"', match.group(1))))


def patch_address(code: bytearray, marker: int, address: int) -> None:
    token = struct.pack("<Q", marker)
    assert code.count(token) == 1, hex(marker)
    index = code.index(token)
    code[index:index + 8] = struct.pack("<Q", address)


def verify_game(path: Path) -> None:
    data = path.read_bytes()
    assert hashlib.sha256(data).hexdigest() == GAME_SHA256, "GameAssembly hash differs"
    image = pefile.PE(data=data, fast_load=True)
    early = data[image.get_offset_from_rva(EARLY_CHECK):][:5]
    assert early.hex().upper() == "E83BA27101", "changed early-check callsite"
    assert EARLY_CHECK + 5 + struct.unpack("<i", early[1:])[0] == GET_PROPERTY_BLOCK
    assert data[image.get_offset_from_rva(EARLY_CHECK - 3):][:3] == bytes.fromhex("488BCF")
    assert data[image.get_offset_from_rva(AFTER_PROPERTY_BLOCK):][:3] == bytes.fromhex("4533C0")
    for site, expected_hex in CALLS.items():
        actual = data[image.get_offset_from_rva(site):][:5]
        assert actual.hex().upper() == expected_hex, f"changed callsite {site:#x}"
        target = site + 5 + struct.unpack("<i", actual[1:])[0]
        assert target == SET_PROPERTY_BLOCK, f"wrong target at {site:#x}"
    start, end = 0x1155460, 0x1157010
    method = data[image.get_offset_from_rva(start):image.get_offset_from_rva(end)]
    decoder = Cs(CS_ARCH_X86, CS_MODE_64)
    found = {ins.address for ins in decoder.disasm(method, start)
             if ins.mnemonic == "call" and ins.op_str == f"{SET_PROPERTY_BLOCK:#x}"}
    assert found == set(CALLS), f"unmapped sustain property-block calls: {found ^ set(CALLS)}"


def verify_stubs(source: str) -> None:
    code_addr, invalidate_addr, early_addr = 0x10000000, 0x10000100, 0x10000200
    table_addr, flag_addr = 0x10001000, 0x10001800
    original_addr, getter_addr = 0x20000000, 0x20001000
    stack_addr, end_addr, skip_addr = 0x30000000, 0x40000000, 0x40000100
    cache, invalidate = stub(source, "CacheStubHex"), stub(source, "InvalidateStubHex")
    early = stub(source, "EarlyCheckStubHex")
    assert len(cache) == 93 and len(invalidate) == 69 and len(early) == 101
    for code in (cache, invalidate):
        patch_address(code, 0x1122334455667788, flag_addr)
        patch_address(code, 0x2233445566778899, table_addr)
        patch_address(code, 0x33445566778899AA, original_addr)
    patch_address(early, 0x1122334455667788, flag_addr)
    patch_address(early, 0x2233445566778899, table_addr)
    patch_address(early, 0x1100FFEEDDCCBBAA, table_addr + 2056)
    patch_address(early, 0x1020304050607080, skip_addr)
    patch_address(early, 0x33445566778899AA, getter_addr)

    vm = Uc(UC_ARCH_X86, UC_MODE_64)
    for address in (code_addr, table_addr, original_addr, getter_addr, stack_addr, end_addr):
        vm.mem_map(address, 4096)
    vm.mem_write(code_addr, bytes(cache))
    vm.mem_write(invalidate_addr, bytes(invalidate))
    vm.mem_write(early_addr, bytes(early))
    vm.mem_write(original_addr, b"\xC3")
    vm.mem_write(getter_addr, b"\xC3")
    original_calls, getter_calls = [0], [0]

    def on_code(_vm, address, _size, _user):
        if address == original_addr:
            original_calls[0] += 1
        if address == getter_addr:
            getter_calls[0] += 1

    vm.hook_add(UC_HOOK_CODE, on_code)

    def invoke(address: int, renderer: int, length_bits: int, destination: int = end_addr) -> None:
        top = stack_addr + 2048
        vm.mem_write(top, struct.pack("<Q", end_addr))
        vm.reg_write(UC_X86_REG_RSP, top)
        vm.reg_write(UC_X86_REG_RCX, renderer)
        vm.reg_write(UC_X86_REG_RDX, 0x51515151)
        vm.reg_write(UC_X86_REG_R8, 0)
        vm.reg_write(UC_X86_REG_XMM12, length_bits)
        vm.emu_start(address, destination)
        assert vm.reg_read(UC_X86_REG_RSP) == top + 8
        assert vm.reg_read(UC_X86_REG_RCX) == renderer
        assert vm.reg_read(UC_X86_REG_RDX) == 0x51515151
        assert vm.reg_read(UC_X86_REG_R8) == 0
        assert vm.reg_read(UC_X86_REG_XMM12) == length_bits

    renderer = 0x50001000
    vm.mem_write(flag_addr, b"\x01")
    invoke(early_addr, renderer, 0x3F800000)
    assert getter_calls[0] == 1, "first regular tail must read its property block"
    invoke(code_addr, renderer, 0x3F800000)
    assert original_calls[0] == 1, "first write must pass through"
    invoke(early_addr, renderer, 0x3F800000, skip_addr)
    assert getter_calls[0] == 1, "repeat must skip the entire property-block sequence"
    invoke(code_addr, renderer, 0x3F800000)
    assert original_calls[0] == 1, "same length must skip"
    assert struct.unpack("<Q", vm.mem_read(table_addr + 2056, 8))[0] == 2
    invoke(invalidate_addr, renderer, 0x3F800000)
    assert original_calls[0] == 2, "held/open invalidation must pass through"
    invoke(early_addr, renderer, 0x3F800000)
    assert getter_calls[0] == 2, "held/open invalidation must force the regular sequence"
    invoke(code_addr, renderer, 0x3F800000)
    assert original_calls[0] == 3, "invalidation must force next regular write"
    invoke(code_addr, renderer, 0x40000000)
    assert original_calls[0] == 4, "changed length must write"
    invoke(early_addr, renderer, 0x3F800000)
    assert getter_calls[0] == 3, "changed length must read the property block"
    vm.mem_write(flag_addr, b"\x00")
    invoke(early_addr, renderer, 0x40000000)
    assert getter_calls[0] == 4, "disabled early bridge must call the original getter"
    invoke(code_addr, renderer, 0x40000000)
    assert original_calls[0] == 5, "disabled bridge must pass through"
    vm.mem_write(flag_addr, b"\x01")
    # Different renderers may hash to the same slot. Each collision must
    # miss safely, even when both tails happen to have the same length.
    collision = renderer + 0x800
    invoke(code_addr, collision, 0x40000000)
    invoke(code_addr, renderer, 0x40000000)
    assert original_calls[0] == 7, "direct-map collisions must not skip writes"
    invoke(code_addr, renderer, 0x40000000)
    assert original_calls[0] == 7, "repeat after collision should skip"
    invoke(early_addr, renderer, 0x40000000, skip_addr)
    assert getter_calls[0] == 4, "repeat after collision should skip the full sequence"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("game_assembly", type=Path)
    args = parser.parse_args()
    verify_game(args.game_assembly)
    source = (Path(__file__).parents[1] / "src/SlopHero.Mod/NativeRegularSustainCache.cs").read_text()
    for address, expected_hex in CALLS.items():
        assert re.search(rf"new\(0x{address:X},\s*\"{expected_hex}\"", source), f"C# callsite {address:#x} differs"
    assert 'new(EarlyCheckRva, "E83BA27101", false)' in source
    verify_stubs(source)
    print("Native sustain callsites and bridge behavior verified.")


if __name__ == "__main__":
    main()
