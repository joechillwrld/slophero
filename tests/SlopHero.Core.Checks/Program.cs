using SlopHero.Core;

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
Console.WriteLine($"Passed {checks} checks.");
