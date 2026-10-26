#!/usr/bin/env python3
"""Read-only exact-build sustain research (requires pefile, capstone, UnityPy)."""
import argparse
import hashlib
import json
from pathlib import Path

import capstone
import pefile
import UnityPy


GAME_HASH = "12bc21e5a88a2fc9abc259d5d67f24835145eabca6c3f863b075ba37ad75f197"
METHODS = {
    "guitar_render_update": (0x115A2F0, 0x590),
    "upcoming_sustain_helper": (0x1156710, 0x890),
    "held_sustain_helper": (0x1155460, 0x930),
    "note_head_state_apply": (0x353210, 0x570),
    "inactive_pool_cleanup": (0x1160A20, 0x80),
}
CALLS = {
    0x2895320: "OnDemandRendering.willCurrentFrameRender",
    0x2870FC0: "Renderer.enabled=",
    0x2885020: "SpriteRenderer.sprite=",
    0x2884F00: "SpriteRenderer.color=",
    0x287AEE0: "GameObject.SetActive",
    0x287B320: "GameObject.activeSelf",
    0x288A510: "Transform.localPosition=",
    0x2870D50: "Renderer.GetPropertyBlock",
    0x2870DE0: "Renderer.SetPropertyBlock",
    0x2865C50: "MaterialPropertyBlock.SetFloat",
    0x2865FE0: "MaterialPropertyBlock.SetVector",
    0x289D090: "SortingGroup.sortingOrder=",
    0x28710E0: "Renderer.sortingOrder=",
}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-assembly", required=True)
    parser.add_argument("--globals-assets", required=True)
    parser.add_argument("--resources-assets", required=True)
    args = parser.parse_args()
    digest = sha(args.game_assembly)
    if digest != GAME_HASH:
        parser.error("GameAssembly fingerprint differs; these offsets must be remapped")
    report = {"sha256": {"GameAssembly.dll": digest,
                         "globalgamemanagers.assets": sha(args.globals_assets),
                         "resources.assets": sha(args.resources_assets)}, "native": {}}
    pe = pefile.PE(args.game_assembly, fast_load=True)
    base = pe.OPTIONAL_HEADER.ImageBase
    disassembler = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    for label, (rva, size) in METHODS.items():
        code = pe.get_data(rva, size)
        calls = []
        for ins in disassembler.disasm(code, base + rva):
            if ins.mnemonic != "call" or not ins.op_str.startswith("0x"):
                continue
            target = int(ins.op_str, 16) - base
            if target in CALLS:
                calls.append({"callsite": hex(ins.address - base), "target": CALLS[target]})
        report["native"][label] = {"rva": hex(rva), "bytes_sha256": hashlib.sha256(code).hexdigest(),
                                   "call_sites_not_execution_counts": calls}
    report["shaders"] = []
    for obj in UnityPy.load(args.globals_assets).objects:
        if obj.type.name != "Shader" or obj.path_id not in (14, 15):
            continue
        form = obj.read_typetree()["m_ParsedForm"]
        report["shaders"].append({"path_id": obj.path_id, "name": form["m_Name"],
            "properties": [prop["m_Name"] for prop in form["m_PropInfo"]["m_Props"]],
            "subshader_tags": [dict(sub["m_Tags"]["tags"]) for sub in form["m_SubShaders"]]})
    report["materials"] = []
    for obj in UnityPy.load(args.resources_assets).objects:
        if obj.type.name != "Material" or obj.path_id not in (25, 26, 29, 30):
            continue
        material = obj.read_typetree()
        report["materials"].append({"path_id": obj.path_id, "name": material["m_Name"],
            "shader_reference": material["m_Shader"],
            "instancing_enabled_in_asset": material["m_EnableInstancingVariants"]})
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
