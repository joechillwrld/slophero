#!/usr/bin/env python3
"""Map ETWAnalyzer GameAssembly instruction rows to Il2CppDumper methods.

ETWAnalyzer's -methods CSV retains GameAssembly instruction RVAs even when
WPA cannot resolve its stack frames. Il2CppDumper's script.json contains the
exact-build managed method entry RVAs, which lets this script label nearby
sampled instruction rows without pretending to reconstruct dropped stacks.
"""

from __future__ import annotations

import argparse
import bisect
import csv
import json
import re
from collections import defaultdict
from decimal import Decimal, InvalidOperation
from pathlib import Path


GAMEASSEMBLY_RVA = re.compile(r"GameAssembly\.dll\+0x([0-9a-fA-F]+)", re.IGNORECASE)
MAPPED_ENTRY_RVA = re.compile(r"^(0x[0-9a-fA-F]+)")


def read_methods(path: Path) -> tuple[list[int], dict[int, list[tuple[str, str]]]]:
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    by_rva: dict[int, list[tuple[str, str]]] = {}
    for method in data.get("ScriptMethod", []):
        try:
            rva = int(method["Address"])
            name = str(method["Name"])
            signature = str(method.get("Signature", ""))
        except (KeyError, TypeError, ValueError):
            continue
        by_rva.setdefault(rva, []).append((name, signature))
    starts = sorted(by_rva)
    return starts, by_rva


def map_ip(ip: int, starts: list[int], methods: dict[int, list[tuple[str, str]]], max_gap: int):
    index = bisect.bisect_right(starts, ip) - 1
    if index < 0:
        return "", "", ""
    start = starts[index]
    delta = ip - start
    next_start = starts[index + 1] if index + 1 < len(starts) else None
    # Large gaps are native/shared stubs or unmapped code, not a safe nearest-
    # symbol match. Keep them explicitly unresolved instead of mislabeling.
    if delta > max_gap or (next_start is None and delta > max_gap):
        return "", "", ""
    candidates = methods[start]
    names = " | ".join(dict.fromkeys(name for name, _ in candidates))
    signatures = " | ".join(dict.fromkeys(sig for _, sig in candidates if sig))
    return names, signatures, f"0x{start:X}+0x{delta:X}"


def as_decimal(value: str | None) -> Decimal:
    if not value:
        return Decimal(0)
    try:
        return Decimal(value.replace(",", "").strip())
    except InvalidOperation:
        return Decimal(0)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("methods_csv", type=Path, help="ETWAnalyzer -dump CPU -methods CSV")
    parser.add_argument("script_json", type=Path, help="exact-build Il2CppDumper script.json")
    parser.add_argument("output_csv", type=Path, help="destination CSV (semicolon-delimited)")
    parser.add_argument(
        "--summary-csv",
        type=Path,
        help="optional flat CPU-weight report grouped by exact mapped method entry RVA",
    )
    parser.add_argument(
        "--max-gap",
        type=lambda value: int(value, 0),
        default=0x2000,
        help="maximum distance from an entry RVA before leaving a row unresolved (default: 0x2000)",
    )
    args = parser.parse_args()
    starts, method_map = read_methods(args.script_json)
    if not starts:
        parser.error("script.json contains no ScriptMethod addresses")

    # ETWAnalyzer prefixes its semicolon CSV with a separator declaration.
    csv.field_size_limit(10_000_000)
    with args.methods_csv.open("r", encoding="utf-8-sig", newline="") as source:
        first = source.readline()
        if not first.lower().startswith("sep = ;"):
            source.seek(0)
        reader = csv.DictReader(source, delimiter=";")
        if not reader.fieldnames or "Method" not in reader.fieldnames:
            parser.error("input is not an ETWAnalyzer methods CSV with a Method column")
        fields = list(reader.fieldnames) + ["Mapped Method", "Mapped Signature", "Mapped RVA"]
        rows = []
        for row in reader:
            match = GAMEASSEMBLY_RVA.search(row.get("Method", ""))
            if match:
                name, signature, mapped_rva = map_ip(
                    int(match.group(1), 16), starts, method_map, args.max_gap
                )
            else:
                name, signature, mapped_rva = "", "", ""
            row["Mapped Method"] = name
            row["Mapped Signature"] = signature
            row["Mapped RVA"] = mapped_rva
            rows.append(row)

    args.output_csv.parent.mkdir(parents=True, exist_ok=True)
    with args.output_csv.open("w", encoding="utf-8", newline="") as output:
        output.write("sep = ;\n")
        writer = csv.DictWriter(output, fieldnames=fields, delimiter=";", extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)
    mapped = sum(bool(row["Mapped Method"]) for row in rows)
    print(f"Mapped {mapped}/{len(rows)} method rows; wrote {args.output_csv}")

    if args.summary_csv:
        # The detailed export can contain one row per instruction RVA.
        # Summing those rows by method entry produces a flat method-weight
        # view, not a call tree or a raw sample stream. Keep it separate from
        # WPA's inclusive parent/child stack weights.
        totals: dict[tuple[str, str, str, str], dict[str, Decimal | int]] = defaultdict(
            lambda: {"CPU ms": Decimal(0), "Wait ms": Decimal(0), "Ready ms": Decimal(0), "RVA rows": 0}
        )
        for row in rows:
            entry = MAPPED_ENTRY_RVA.match(row.get("Mapped RVA", ""))
            if not entry:
                continue
            key = (
                row.get("Module", ""),
                entry.group(1).upper(),
                row.get("Mapped Method", ""),
                row.get("Mapped Signature", ""),
            )
            total = totals[key]
            for column in ("CPU ms", "Wait ms", "Ready ms"):
                if column in row:
                    total[column] = total[column] + as_decimal(row[column])
            total["RVA rows"] = int(total["RVA rows"]) + 1

        fields = ["Module", "Method Entry RVA", "Mapped Method", "Mapped Signature", "CPU ms", "Wait ms", "Ready ms", "RVA rows"]
        args.summary_csv.parent.mkdir(parents=True, exist_ok=True)
        with args.summary_csv.open("w", encoding="utf-8", newline="") as output:
            output.write("sep = ;\n")
            writer = csv.DictWriter(output, fieldnames=fields, delimiter=";")
            writer.writeheader()
            ordered = sorted(totals.items(), key=lambda item: Decimal(item[1]["CPU ms"]), reverse=True)
            for (module, rva, name, signature), values in ordered:
                writer.writerow({
                    "Module": module,
                    "Method Entry RVA": rva,
                    "Mapped Method": name,
                    "Mapped Signature": signature,
                    **{column: str(values[column]) for column in ("CPU ms", "Wait ms", "Ready ms")},
                    "RVA rows": values["RVA rows"],
                })
        print(f"Wrote flat method CPU-weight report: {args.summary_csv}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
