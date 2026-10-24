# Profiling Clone Hero 1.1.0.6142 on Windows 10

The user has already supplied WPA stack and method exports and a matching
UnityPlayer PDB. Consult [performance-handoff.md](performance-handoff.md)
before requesting another export; this guide is for a new, specific capture.

This workflow measures the shipped game without changing its files or
controller polling. The supplied build uses Unity 2022.3.62. The retail player
may not have Unity Profiler connection support enabled, so Windows ETW is the
primary capture path; it can still show native CPU stacks, GPU activity, and
frame presentation.

## Optional check: Unity Profiler

Do not install a multi-gigabyte Unity Editor just for this check. If you
already have Unity Hub and the Profiler window, open **Window → Analysis →
Profiler** and check the **Active Profiler** list while Clone Hero is running.
Unity Profiler normally attaches to development players with profiler support
enabled; a retail game often will not appear. The matching editor version
would be 2022.3.62 if you already have it.

If Clone Hero appears, select it, open **CPU Usage**, choose **Timeline**, and
leave **Deep Profile** off. Start a capture after gameplay has settled, record
about 20 seconds, then save the Profiler data. Deep Profile instruments managed
calls and can distort the very timing we are trying to measure.

If Clone Hero does not appear in the player list, use ETW below. Do not replace
game files or install a modified player to force Profiler access.

## Primary capture: Windows Performance Recorder and Analyzer

Install only **Windows Performance Toolkit** from the Windows ADK. It includes
Windows Performance Recorder (WPR), Windows Performance Analyzer (WPA), and
GPUView; no other ADK components are needed. In the installer, select the
Windows Performance Toolkit feature and leave the other features unchecked.

1. Close overlays or background captures that are not part of the normal setup.
   Keep Clone Hero's resolution, frame limit, chart speed, mods, controller,
   and SlopHero settings fixed for the whole capture. Leave the controller at
   its normal 1000 Hz polling rate.
2. Start Clone Hero, load one repeatable chart, and wait about 10 seconds for
   loading and warm-up activity to settle.
3. Run **Windows Performance Recorder** as administrator. Select the **General**
   scenario, **Verbose** detail, and **File** logging. Enable **CPU Usage** and
   **GPU Activity** if those profiles are listed. Start recording.
4. Play the same section normally for 20–30 seconds without opening menus or
   changing settings. Stop recording and save the `.etl` trace.
5. Open the trace in WPA. Add **CPU Usage (Sampled)** and **GPU Utilization**.
   In CPU Usage, group by **Process → Thread → Stack**, select Clone Hero's
   main and render threads, and sort by **Weight**. This shows where native CPU
   samples accumulate. If GameAssembly frames appear only as addresses, export
   the module-relative offsets too; the current binary can then be matched
   against its native disassembly. GPU Activity shows whether the game is
   submitting work or waiting on the GPU. Export the selected tables to CSV.

WPR's sampled stacks have much less overhead than Unity Deep Profile. If a
capture shows unexpected stutter, make one short repeat before drawing a
conclusion; ETW collection itself can add a small amount of work.

## Export tables when WPA has no table-export menu

The command-line **WPAExporter.exe** is included with Windows Performance
Toolkit; it is not a separate ADK feature. In WPA, add the desired tables to an
analysis layout, then use **Profiles → Export** to save that layout as a
`.wpaProfile`. In PowerShell, run the exporter against the trace and profile:

```powershell
$tool = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\Windows Performance Toolkit'
$trace = 'C:\path\to\CloneHero.etl'
$profile = 'C:\path\to\SloptifineCapture.wpaProfile'
$output = 'C:\path\to\CSV-output'
& (Join-Path $tool 'WPAExporter.exe') -i $trace -profile $profile -outputfolder $output
```

The exporter writes the profile's tables into the output folder, but a saved
profile may lose the grid's expanded process/thread/stack rows. Check the CPU
CSV's `Stack` column before sharing it: a short file containing only process
totals with blank `Stack` values is not a symbolized stack export. Do not
repeat the trace or profile export just to recreate those same blank rows. If
`WPAExporter.exe` is missing, modify the Windows ADK installation and select
**Windows Performance Toolkit**. The ETL stays on the PC; share only the
resulting CSV files.

### If the CSV has blank Thread and Stack values

Do not record another ETL or rerun WPAExporter. The `.wpaProfile` saves the
table grouping but does not preserve the expanded stack rows, so rerunning the
same command can produce only process totals. Open the existing ETL in WPA,
expand the **Clone Hero.exe** thread and stack rows in **CPU Usage (Sampled)**,
and copy the expanded table from the grid. Save the clipboard as tab-separated
text in PowerShell:

```powershell
Get-Clipboard -Raw | Set-Content -LiteralPath 'D:\Downloads\Sloptifine-symbolized.tsv' -Encoding UTF8
```

Check the saved file for a function name visible in WPA, such as
`UnityPlayer.dll!PlayerRender`. This exports only rows copied from the current
grid; collapsed branches are not included. The earlier process-only CSVs have
blank stack columns and cannot identify native callers.

## Optional presentation trace

PresentMon can record per-present frame times alongside the CPU trace. Use its
process filter for `Clone Hero.exe` and capture the same 20–30 second interval.
This distinguishes actual presentation cadence from Unity callback spacing in
`Latest.log`; it does not identify CPU hotspots by itself.

## What to share

The most useful first pass is the WPA **CPU Usage (Sampled)** table exported to
CSV, the **GPU Utilization** table, and a PresentMon CSV if available. An `.etl`
trace is useful too if it is small enough to upload. A Unity Profiler `.data`
capture is optional if Unity successfully attaches. Include the chart name,
solo/online mode, `renderFrameInterval`, resolution, chart speed, whether the
run was practice, and whether SlopHero/ClonZones were loaded.

Start with the normal setup rather than changing or removing mods. That tells
us which native game threads and methods dominate the real workload. Once we
have that baseline, we can compare one reversible setting at a time. No
controller-polling changes are part of this profiling plan.
