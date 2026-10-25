#!/usr/bin/env python3
"""Summarize raw Intel PresentMon per-frame CSVs for repeatable A/B captures.

Usage: python tools/presentmon_report.py baseline.csv candidate.csv
       python tools/presentmon_report.py --fps-windows capture.csv
The -stats.csv summary is insufficient: this tool needs the raw per-frame file.
"""

import argparse
import csv
import math
from collections import Counter
from pathlib import Path


METRICS = (
    "MsBetweenPresents",
    "MsBetweenDisplayChange",
    "MsCPUBusy",
    "MsCPUWait",
    "MsGPUBusy",
    "MsGPUWait",
    "MsInPresentAPI",
    "GPUUtilization",
)


def number(value):
    try:
        result = float(value)
        return result if math.isfinite(result) else None
    except (TypeError, ValueError):
        return None


def percentile(values, fraction):
    if not values:
        return None
    ordered = sorted(values)
    return ordered[max(0, math.ceil(len(ordered) * fraction) - 1)]


def describe(values):
    if not values:
        return "n/a"
    return (f"mean={sum(values) / len(values):.3f} "
            f"p50={percentile(values, .50):.3f} "
            f"p95={percentile(values, .95):.3f} "
            f"p99={percentile(values, .99):.3f} "
            f"max={max(values):.3f}")


def analyze(path):
    values = {name: [] for name in METRICS}
    modes, chains, processes = Counter(), Counter(), Counter()
    fps_windows = Counter()
    spikes = []
    first_time = last_time = None
    rows = 0
    with path.open(newline="", encoding="utf-8-sig") as stream:
        reader = csv.DictReader(stream)
        if not reader.fieldnames or "MsBetweenPresents" not in reader.fieldnames:
            raise ValueError(f"{path}: expected a raw PresentMon per-frame CSV")
        for row in reader:
            rows += 1
            modes[row.get("PresentMode", "")] += 1
            chains[row.get("SwapChainAddress", "")] += 1
            processes[(row.get("Application", ""), row.get("ProcessID", ""))] += 1
            timestamp = number(row.get("TimeInSeconds"))
            if timestamp is not None:
                first_time = timestamp if first_time is None else first_time
                last_time = timestamp
                fps_windows[int(timestamp - first_time)] += 1
            for name in METRICS:
                metric = number(row.get(name))
                if metric is not None:
                    values[name].append(metric)
            frame = number(row.get("MsBetweenPresents"))
            if frame is not None and frame >= 3:
                spikes.append((timestamp, frame, number(row.get("MsCPUBusy")),
                               number(row.get("MsGPUBusy"))))
    if rows == 0 or not values["MsBetweenPresents"]:
        raise ValueError(f"{path}: no frame timings found")
    duration = (last_time - first_time) if first_time is not None and last_time is not None else None
    return rows, duration, values, modes, chains, processes, spikes, fps_windows


def fmt(value):
    return "n/a" if value is None else f"{value:.3f}"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fps-windows", action="store_true",
                        help="show actual presents counted in each complete one-second window")
    parser.add_argument("csv", nargs="+", type=Path, help="raw pmcap CSV, not -stats.csv")
    args = parser.parse_args()
    for path in args.csv:
        rows, duration, values, modes, chains, processes, spikes, fps_windows = analyze(path)
        print(f"\n{path.name}: {rows} frames, {fmt(duration)} s, "
              f"{fmt(rows / duration if duration and duration > 0 else None)} presents/s")
        print(f"  process={dict(processes)}; present mode={dict(modes)}; swapchains={dict(chains)}")
        for name in METRICS:
            print(f"  {name}: {describe(values[name])}")
        print("  frames >=2/3/5/10 ms: " + "/".join(
            str(sum(v >= limit for v in values["MsBetweenPresents"]))
            for limit in (2, 3, 5, 10)))
        if args.fps_windows and duration is not None:
            for second in range(int(duration)):
                print(f"  presented FPS {second:>3}-{second + 1:<3} s: "
                      f"{fps_windows[second]} presents/s")
        for timestamp, frame, cpu, gpu in sorted(spikes, key=lambda row: row[1], reverse=True)[:10]:
            print(f"  spike at {fmt(timestamp)} s: present={fmt(frame)} ms, "
                  f"CPU busy={fmt(cpu)} ms, GPU busy={fmt(gpu)} ms")
        if len(chains) > 1 or len(processes) > 1:
            print("  CAUTION: mixed processes/swapchains; split the capture before A/B comparison.")
    print("\nCompare matched runs only. CPU/GPU busy can overlap; do not add them. "
          "MsGPUTime includes wait and is not GPU work.")


if __name__ == "__main__":
    main()
