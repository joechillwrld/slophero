using SlopHero.Core;
using SlopHero.Mod;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    checks++;
}
var hold = new HoldTrigger();
Check(!hold.Update(true, true, 0, .75), "Initial press");
Check(!hold.Update(true, true, .5, .75), "Short hold");
Check(hold.Update(true, true, .75, .75), "Threshold fires");
Check(!hold.Update(true, true, 2, .75), "Continued hold does not repeat");
hold.Update(false, true, 3, .75);
hold.Update(true, true, 4, .75);
Check(hold.Update(true, true, 5, .75), "Release rearms");
hold.RequireRelease();
Check(!hold.Update(true, true, 10, .75), "Scene reset requires release");
hold.Update(false, true, 11, .75);
hold.Update(true, false, 12, .75);
Check(!hold.Update(true, true, 14, .75), "Held button through pause stays blocked");
hold.Update(false, true, 15, .75);
hold.Update(true, true, 16, .75);
Check(!hold.Update(true, true, 1, .75), "Clock reversal stays blocked");
Check(!hold.Update(true, true, 3, .75), "Clock reversal requires release");
var stats = new LiveStats(90, 10, 7);
Check(stats.HitPercentage == 90, "Hit percentage");
Check(stats.Misses == 10 && stats.GhostInputs == 7, "Separate miss and ghost counts");
Check(new LiveStats(0, 0, 10).HitPercentage is null, "No notes is undefined");
Check(new LiveStats(10, 0, 100).HitPercentage == 100, "Ghosts do not lower hit percentage");
Check(new LiveStats(0, 10, 0).HitPercentage == 0, "All misses");
Check(new LiveStats(long.MaxValue, long.MaxValue, 0).HitPercentage == 50, "No integer overflow");
Check(ChartNote.CountsTowardAccuracy(1), "Chord head counts");
Check(ChartNote.CountsTowardAccuracy(4), "Ordinary chord child counts");
Check(!ChartNote.CountsTowardAccuracy(6 | 16), "Disjoint chord child omitted");
Check(!ChartNote.IsResolved(0) && !ChartNote.IsMissed(0), "Future note omitted");
Check(ChartNote.IsResolved(1) && !ChartNote.IsMissed(1), "Hit head resolved");
Check(ChartNote.IsResolved(2) && ChartNote.IsMissed(2), "Missed head resolved");
Check(!ChartNote.IsResolved(4 | 8 | 16), "Dynamics and sustain flags are not head outcomes");
Check(!PracticeReadiness.CanSelect(false, true, true, true, true, 0),
    "Regression: practice cannot select before scene initialization even when game flags are ready");
Check(!PracticeReadiness.CanSelect(true, false, true, true, true, 0),
    "Practice cannot select using the outgoing game manager");
Check(!PracticeReadiness.CanSelect(true, true, true, true, false, 0),
    "Regression: missing beat renderer resources block the native practice call");
Check(!PracticeReadiness.CanSelect(true, true, true, false, true, 0),
    "Section menu must finish initialization");
Check(!PracticeReadiness.CanSelect(true, true, false, true, true, 0),
    "Practice player and chart must finish loading");
Check(!PracticeReadiness.CanSelect(true, true, true, true, true, 1),
    "Initialization coroutines block selection");
Check(PracticeReadiness.CanSelect(true, true, true, true, true, 0),
    "Fully initialized practice scene allows selection");
var speed = new ScrollSpeedCompensation();
Check(speed.Update(10, .5f, true) == 20, "Half game-clock rate compensates inferred renderer speed");
Check(speed.Update(20, .5f, true) == 20, "Repeated frames preserve compensated speed");
Check(speed.Update(20, 2, true) == 5, "Fast game-clock rate compensates inferred renderer speed");
Check(speed.Update(10, 1, false) == 10, "Toggle off restores unmodified speed");
Check(speed.Update(12, .5f, true) == 24, "New vanilla speed setting replaces baseline");
Check(speed.Update(12, 0, true) == 12, "Invalid tempo restores speed without dividing by zero");
Check(speed.Update(14, 1, false) == 14, "Disabled compensation preserves vanilla changes");
Check(speed.Update(14, float.NaN, true) == 14, "Nonfinite tempo leaves normal speed intact");
var profileSpeed = new ScrollSpeedCompensation();
Check(profileSpeed.Update(5, .5f, true, 10) == 20, "Half game-clock rate uses configured profile speed");
Check(profileSpeed.Update(15, 1.5f, true, 10) == 10f / 1.5f, "Fast game-clock rate compensates without replacing profile baseline");
Check(profileSpeed.Update(10, .5f, true, 12) == 24, "Live profile speed change is reflected during practice");
Check(profileSpeed.Update(24, .5f, false, 12) == 12, "Disabling restores latest configured speed");
Check(profileSpeed.Update(18, 1.5f, true, 12) == 8, "Enable after speedup uses configured baseline");
var nonDefaultSpeed = new ScrollSpeedCompensation();
Check(nonDefaultSpeed.Update(17, .5f, true, 17) == 34, "Compensation supports configured speed 17 at 50% clock rate");
Check(nonDefaultSpeed.Update(17, 1.4f, true, 17) == 17f / 1.4f, "Compensation supports configured speed 17 at 140% clock rate");
Check(nonDefaultSpeed.Update(31, 1.95f, true, 31) == 31f / 1.95f, "Compensation supports configured speed 31 at 195% clock rate");
Check(NoteVisibilityWindow.Calculate(3, 10, 20) == 1.5, "Faster speed preserves the same world-space visibility distance");
Check(NoteVisibilityWindow.Calculate(3, 10, 5) == 6, "Slower visual speed expands lookahead");
Check(NoteVisibilityWindow.Calculate(3, 10, 10) == 3, "Normal speed preserves native window");
Check(double.IsNaN(NoteVisibilityWindow.Calculate(0, 10, 20)), "Zero initialized window cannot hide notes");
Check(double.IsNaN(NoteVisibilityWindow.Calculate(-3, 10, 20)), "Negative lookahead rejected");
Check(double.IsNaN(NoteVisibilityWindow.Calculate(3, 10, 0)), "Zero speed rejected");
Check(double.IsNaN(NoteVisibilityWindow.Calculate(3, 10, float.NaN)), "Nonfinite speed rejected");
long[] ticks = { 0, 10, 10, 20, 30 };
Check(OrderedNoteSearch.LowerBound(ticks.Length, 10, i => ticks[i]) == 1, "Practice includes all heads at the section boundary");
Check(OrderedNoteSearch.LowerBound(ticks.Length, 11, i => ticks[i]) == 3, "Practice skips earlier heads");
Check(OrderedNoteSearch.LowerBound(ticks.Length, 31, i => ticks[i]) == ticks.Length, "Range after chart returns end");
Check(OrderedNoteSearch.LowerBound(0, 10, _ => throw new Exception()) == 0, "Empty chart needs no native reads");
int reads = 0;
Check(OrderedNoteSearch.LowerBound(100000, 99000, i => { reads++; return i; }) == 99000 && reads < 20,
    "Large practice seek needs logarithmic native reads");
var corrupt = new ScrollSpeedCompensation();
Check(corrupt.Update(23, 1.95f, true, 1344637400) == 23, "Regression: pointer-like configured speed leaves native speed intact");
Check(corrupt.Update(23, 1.95f, true, 23) == 23f / 1.95f, "Profile speed uses measured clock-rate compensation");
Check(corrupt.Update(23, 1.95f, true, float.PositiveInfinity) == 23,
    "Corrupt setting while active restores native baseline");
Check(corrupt.Update(23, float.Epsilon, true, 23) == 23, "Extreme tempo cannot overflow into disappearing notes");
var clockRate = new PracticeClockRate();
Check(float.IsNaN(clockRate.Update(0, 0, true)), "Clock rate waits for a playback interval");
for (int i = 1; i <= 5; i++)
    clockRate.Update(i * .05 * 1.35, i * .05, true);
Check(Math.Abs(clockRate.Update(.405, .3, true) - 1.35f) < .0001f,
    "Practice compensation estimates the actual gameplay clock rate");
Check(float.IsNaN(clockRate.Update(.405, .3, false)), "Clock-rate estimator resets outside active play");
IntPtr dataBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(64);
IntPtr profileBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(64);
IntPtr settingBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(64);
try
{
    System.Runtime.InteropServices.Marshal.WriteIntPtr(dataBuffer, 0x10, profileBuffer);
    System.Runtime.InteropServices.Marshal.WriteIntPtr(profileBuffer, 0x20, settingBuffer);
    System.Runtime.InteropServices.Marshal.WriteInt32(settingBuffer, 0x10, 23);
    Check(ConfiguredNoteSpeed.Read(dataBuffer) == 23, "Concrete native pointer chain reads raw speed, not object header");
    System.Runtime.InteropServices.Marshal.WriteInt32(settingBuffer, 0x10, 31);
    Check(ConfiguredNoteSpeed.Read(dataBuffer) == 31, "Live profile edits are read through concrete layout");
    System.Runtime.InteropServices.Marshal.WriteInt32(settingBuffer, 0x10, 1344637400);
    Check(float.IsNaN(ConfiguredNoteSpeed.Read(dataBuffer)), "Native reader rejects observed corrupt magnitude");
    System.Runtime.InteropServices.Marshal.WriteIntPtr(profileBuffer, 0x20, IntPtr.Zero);
    Check(float.IsNaN(ConfiguredNoteSpeed.Read(dataBuffer)), "Missing setting pointer leaves rendering alone");
    Check(float.IsNaN(ConfiguredNoteSpeed.Read(IntPtr.Zero)), "Absent player data leaves rendering alone");
}
finally
{
    System.Runtime.InteropServices.Marshal.FreeHGlobal(settingBuffer);
    System.Runtime.InteropServices.Marshal.FreeHGlobal(profileBuffer);
    System.Runtime.InteropServices.Marshal.FreeHGlobal(dataBuffer);
}
Check(ChartSpeedLabel.Format(1.0000002f) == "100%", "Native floating-point noise does not show as chart speed drift");
Check(ChartSpeedLabel.Format(.75f) == "75%", "Slowed chart display");
Check(ChartSpeedLabel.Format(1.5f) == "150%", "Sped-up chart display");
Check(ChartSpeedLabel.Format(1.9500008f) == "195%", "Observed practice tempo display");
Check(ChartSpeedLabel.Format(1.125f) == "112.5%", "Fractional chart speed display");
Check(ChartSpeedLabel.Format(float.NaN) == "—" && ChartSpeedLabel.Format(0) == "—", "Invalid tempo display");
Check(SongTitleLabel.Format("Song Title", 1.5f, true) == "Song Title (150%)", "Speed appears beside song title");
Check(SongTitleLabel.Format("Song Title", .75f, true) == "Song Title (75%)", "Slowed speed appears beside song title");
Check(SongTitleLabel.Format("Song Title", 1.5f, false) == "Song Title", "Hidden speed leaves title unchanged");
Check(SongTitleLabel.Format("", 1.5f, true) == "", "Speed is not shown without a title");
Check(NativeRingState.IsPerfectFc(0), "Native perfect FC hue selects perfect ring color");
Check(NativeRingState.IsPerfectFc(.00001f), "Native perfect FC hue tolerates float noise");
Check(!NativeRingState.IsPerfectFc(1f / 6f), "Native regular FC hue selects regular ring color");
Check(!NativeRingState.IsPerfectFc(.35f), "Unrelated native ring hue is not classified as perfect FC");
Check(!NativeRingState.IsPerfectFc(float.NaN), "Invalid native ring hue is not perfect FC");
string profileRoot = Path.Combine(Path.GetTempPath(), "SlopHero-profile-check-" + Guid.NewGuid().ToString("N"));
try
{
    var profiles = new SettingsProfiles(profileRoot);
    var settings = new Dictionary<string, string> { ["FcColor"] = "FF8800", ["ShowHud"] = "true" };
    profiles.Save("Warm Theme", settings);
    Check(profiles.List().SequenceEqual(new[] { "Warm Theme" }), "Profile list returns saved file names");
    Check(profiles.TryLoad("Warm Theme", out var loaded) && loaded.Count == 2
        && loaded["FcColor"] == "FF8800" && loaded["ShowHud"] == "true", "Profile JSON round trips settings");
    bool rejected = false;
    try { profiles.Save("../outside", settings); } catch (FormatException) { rejected = true; }
    Check(rejected && !File.Exists(Path.Combine(profileRoot, "outside.json")), "Profile names cannot escape the profile folder");
}
finally { if (Directory.Exists(profileRoot)) Directory.Delete(profileRoot, true); }
Console.WriteLine($"Passed {checks} checks.");
