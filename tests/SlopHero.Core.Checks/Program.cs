using SlopHero.Core;
using SlopHero.Mod;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    checks++;
}
Check(RainbowColorCurve.Shape(.5f, 1f, 1f, 1f) == .5f,
    "Default rainbow channel is unchanged");
Check(RainbowColorCurve.Shape(.25f, 2f, 1.5f, 2f) > .2f,
    "High color sliders retain an intermediate orange channel");
Check(RainbowColorCurve.Shape(0f, 2f, 1.5f, 2f) == 0f,
    "High color sliders do not introduce an unwanted third channel");
Check(RainbowColorCurve.Shape(1f, 2f, 1.5f, 2f) <= 1.2f,
    "High brightness stays below the previous twofold HDR clipping level");
Check(RainbowNoteVariation.PaletteIndex(2, 0, 0f) == 10,
    "Zero note variety preserves the lane palette");
Check(RainbowNoteVariation.PaletteIndex(2, 0, 1f) == RainbowNoteVariation.PaletteIndex(2, 0, 1f)
    && RainbowNoteVariation.PaletteIndex(2, 0, 1f) != RainbowNoteVariation.PaletteIndex(2, 1, 1f),
    "Note variety is stable for a pool slot and differs across slots");
Check(Enumerable.Range(0, 60).Select(slot => RainbowNoteVariation.PaletteIndex(2, slot, 1f)).Distinct().Count() == 60,
    "Full note variety covers the complete hue palette");
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
var lateHits = LiveStats.FromResolvedHeads(617, 627, 96);
Check(lateHits.Misses == 10 && Math.Abs(lateHits.HitPercentage!.Value - 61700d / 627) < .000001,
    "Late hits use the game's resolved-head total minus native hits");
Check(LiveStats.FromResolvedHeads(12, 10, 0).Misses == 0,
    "Native hits can run ahead of a bounded chart scan without showing negative misses");
Check(ChartNote.CountsTowardAccuracy(1), "Chord head counts");
Check(ChartNote.CountsTowardAccuracy(4), "Ordinary chord child counts");
Check(!ChartNote.CountsTowardAccuracy(6 | 16), "Disjoint chord child omitted");
Check(!ChartNote.IsResolved(0) && !ChartNote.IsMissed(0), "Future note omitted");
Check(ChartNote.IsResolved(1) && !ChartNote.IsMissed(1), "Hit head resolved");
Check(ChartNote.IsResolved(2) && ChartNote.IsMissed(2), "Missed head resolved");
Check(!ChartNote.IsResolved(4 | 8 | 16), "Dynamics and sustain flags are not head outcomes");
const float networkTickInterval = 1f / 30f;
Check(Math.Abs(NetworkTickRemainder.PreserveFraction(.035f, networkTickInterval) - (.035f % networkTickInterval)) < .000001f,
    "Network tick experiment keeps the fractional remainder after a normal overrun");
Check(NetworkTickRemainder.PreserveFraction(networkTickInterval, networkTickInterval) == 0f,
    "Exact network tick boundaries leave no remainder");
Check(NetworkTickRemainder.PreserveFraction(.105f, networkTickInterval) < networkTickInterval,
    "Long network stalls discard whole missed periods instead of accumulating catch-up bursts");
Check(NetworkTickRemainder.PreserveFraction(float.NaN, networkTickInterval) == 0f
    && NetworkTickRemainder.PreserveFraction(.01f, 0f) == 0f,
    "Invalid network tick timing fails open to a zero remainder");
Check(Convert.ToHexString(NativeRendererFilterStub.Build(new IntPtr(0x200000), new IntPtr(0x300000), new IntPtr(0x400000)))
    == "49BA000020000000000041803A0074324989CA49C1EA044181E2FF01000049BB00003000000000004B390CD374134B833CD300740D41FFC24181E2FF010000EBE7C349BB000040000000000041FFE3",
    "Native renderer filter stub keeps its tested pointer lookup, pass-through, and branch layout");
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
bool MouseSamplerScope(bool requested = true, bool ready = true, bool playing = true,
    bool paused = false, bool songOver = false, int coroutines = 0, int players = 1,
    int mode = 0, bool setlist = false, bool transition = false, bool menu = false,
    bool focused = true, bool online = false)
    => OfflineSoloSongScope.IsEligible(requested, ready, playing, paused, songOver,
        coroutines, players, mode, setlist, transition, menu, focused, online);
Check(MouseSamplerScope(), "Mouse sampler is eligible in active offline solo Quickplay");
Check(!MouseSamplerScope(requested: false), "Mouse sampler preference defaults off");
Check(!MouseSamplerScope(online: true), "Mouse sampler never suppresses online play");
Check(!MouseSamplerScope(players: 2), "Mouse sampler leaves multiplayer polling intact");
Check(!MouseSamplerScope(mode: 1), "Mouse sampler is limited to guitar game mode");
Check(!MouseSamplerScope(setlist: true), "Mouse sampler leaves setlists intact");
Check(!MouseSamplerScope(paused: true), "Mouse sampler resumes while paused");
Check(!MouseSamplerScope(songOver: true), "Mouse sampler resumes at song end");
Check(!MouseSamplerScope(coroutines: 1), "Mouse sampler resumes during transitions");
Check(!MouseSamplerScope(transition: true), "Mouse sampler resumes during mod transitions");
Check(!MouseSamplerScope(menu: true), "Mouse sampler resumes while SlopHero settings are open");
Check(!MouseSamplerScope(focused: false), "Mouse sampler resumes when the game loses focus");
Check(!MouseSamplerScope(ready: false), "Mouse sampler fails open until game state is ready");
var shortcutPolling = new ShortcutPollingGate();
Check(shortcutPolling.ShouldPoll(true, true, 0, "Key:R", "Key:P"),
    "Throttled hotkeys sample immediately when song play starts");
Check(!shortcutPolling.ShouldPoll(true, true, .004, "Key:R", "Key:P"),
    "Throttled hotkeys skip redundant checks inside the 120 Hz interval");
Check(shortcutPolling.ShouldPoll(true, true, 1d / 120d, "Key:R", "Key:P"),
    "Throttled hotkeys sample again at 120 Hz");
Check(shortcutPolling.ShouldPoll(true, true, 1d / 120d + .001, "Key:T", "Key:P"),
    "Changing a hotkey binding refreshes its sampled state immediately");
Check(shortcutPolling.ShouldPoll(false, true, 1d / 120d + .002, "Key:T", "Key:P"),
    "Disabling the throttle restores per-update shortcut checks immediately");
Check(shortcutPolling.ShouldPoll(true, true, 1d / 120d + .003, "Key:T", "Key:P"),
    "Enabling the throttle starts with an immediate hotkey sample");
Check(!shortcutPolling.ShouldPoll(true, false, 1d / 120d + .004, "Key:T", "Key:P"),
    "Hotkey samples stop outside the shortcut context");
Check(shortcutPolling.ShouldPoll(true, true, 1d / 120d + .005, "Key:T", "Key:P"),
    "Re-entering gameplay samples the current hotkey state immediately");
var unthrottledShortcuts = new ShortcutPollingGate();
Check(unthrottledShortcuts.ShouldPoll(false, true, 0, "Key:R", "Key:P")
    && unthrottledShortcuts.ShouldPoll(false, true, .001, "Key:R", "Key:P"),
    "Default shortcut behavior remains per-update when throttling is off");
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
var speedEntry = new SongSpeedEntry();
speedEntry.Append(1); speedEntry.Append(5); speedEntry.Append(0);
Check(speedEntry.Text == "150" && speedEntry.TryGetSpeed(out int enteredSpeed) && enteredSpeed == 150,
    "Speed entry shows and applies digits as they are typed");
speedEntry.Backspace();
Check(speedEntry.Text == "15" && speedEntry.TryGetSpeed(out enteredSpeed) && enteredSpeed == 15,
    "Backspace edits the live speed entry");
speedEntry.Backspace(); speedEntry.Backspace();
Check(speedEntry.Text.Length == 0 && !speedEntry.TryGetSpeed(out _), "Speed entry can be cleared without forcing the minimum speed");
speedEntry.Append(5); speedEntry.Append(0); speedEntry.Append(0); speedEntry.Append(0);
Check(speedEntry.TryGetSpeed(out enteredSpeed) && enteredSpeed == 5000, "Speed entry accepts the game's 5000% limit");
speedEntry.Append(1);
Check(speedEntry.Text == "5000", "Speed entry remains bounded to four digits");
speedEntry.Clear(); speedEntry.Append(0);
Check(!speedEntry.TryGetSpeed(out _), "Zero is visible during entry but not applied as a speed");
speedEntry.Clear(); speedEntry.Append(1);
Check(speedEntry.Text == "1" && !speedEntry.TryGetSpeed(out _),
    "Unsupported sub-10% entry remains visible while typing but is not applied");
speedEntry.Append(0);
Check(speedEntry.Text == "10" && speedEntry.TryGetSpeed(out enteredSpeed) && enteredSpeed == 10,
    "Typing the zero commits the supported 10% minimum");
speedEntry.Clear(); speedEntry.Append(5); speedEntry.Append(0); speedEntry.Append(0); speedEntry.Append(1);
Check(!speedEntry.TryGetSpeed(out _), "Speed entry does not apply values above 5000");
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
    Check(profiles.Rename("Warm Theme", "Autumn Colors") == "Autumn Colors"
        && profiles.List().SequenceEqual(new[] { "Autumn Colors" }), "Profile rename updates the on-disk file name");
    Check(profiles.TryLoad("Autumn Colors", out loaded) && loaded["FcColor"] == "FF8800", "Renamed profile keeps its settings");
    profiles.Save("Existing", settings);
    bool duplicateRejected = false;
    try { profiles.Rename("Autumn Colors", "Existing"); } catch (IOException) { duplicateRejected = true; }
    Check(duplicateRejected && profiles.List().Length == 2, "Rename does not overwrite an existing profile");
    bool rejected = false;
    try { profiles.Save("../outside", settings); } catch (FormatException) { rejected = true; }
    Check(rejected && !File.Exists(Path.Combine(profileRoot, "outside.json")), "Profile names cannot escape the profile folder");
}
finally { if (Directory.Exists(profileRoot)) Directory.Delete(profileRoot, true); }
Console.WriteLine($"Passed {checks} checks.");
