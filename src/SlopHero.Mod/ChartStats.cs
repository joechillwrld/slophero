using Il2Cpp;
using SlopHero.Core;
using Engine = Il2Cpp.ObjectPublicAbstractDoBoDoInBoObDoInSiBoUnique;

namespace SlopHero.Mod;

internal sealed class ChartStats
{
    private IntPtr enginePointer, notesPointer;
    private int cursor;
    private long resolvedHeads, rangeStart, rangeEnd;
    private long practiceMissedHeads;
    private readonly List<int> recentPracticeMisses = new();
    private double lastSongTime = double.NegativeInfinity;
    private int lastHits;
    private IntPtr outroEnginePointer, outroNotesPointer;
    private int outroCursor, outroComponents, outroPlayableComponents, outroBeyondSong;
    private double outroLastSongTime = double.NegativeInfinity, lastPlayableTailEnd;
    private bool outroTimingValid = true;
    internal double LastPlayableTailEnd => lastPlayableTailEnd;
    internal int OutroScannedComponents => outroComponents;
    internal int OutroPlayableComponents => outroPlayableComponents;
    internal int OutroBeyondSong => outroBeyondSong;
    internal bool OutroTimingValid => outroTimingValid;

    internal bool AllPlayableNotesDone(Engine engine, GameManager game)
    {
        // Accuracy excludes disjoint child heads, but a child can carry a
        // longer tail. The skip gate checks every chart component separately.
        var notes = engine.prop_List_1_ObjectPublicObInObDoSiDoUIInBoInUnique_0;
        if (engine.Pointer != outroEnginePointer || notes.Pointer != outroNotesPointer
            || game.songTime < outroLastSongTime - .05 || outroCursor > notes.Count)
        {
            outroEnginePointer = engine.Pointer;
            outroNotesPointer = notes.Pointer;
            outroCursor = outroComponents = outroPlayableComponents = outroBeyondSong = 0;
            lastPlayableTailEnd = 0;
            outroTimingValid = true;
        }
        outroLastSongTime = game.songTime;
        for (int budget = 4096; outroCursor < notes.Count && budget > 0; budget--)
        {
            var note = notes[outroCursor];
            if (note == null) { outroTimingValid = false; break; }
            // Every chart component contributes its tail, including disjoint
            // children. Judgment flags can remain unresolved after the final
            // playable moment, so they must not block an otherwise safe skip.
            double noteTime = note.field_Public_Double_0;
            double sustainLength = note.field_Public_Double_1;
            if (!double.IsFinite(noteTime) || !double.IsFinite(sustainLength)
                || sustainLength < 0)
                outroTimingValid = false;
            else if (noteTime > game.songLength)
                // Chart entries after the game's song-end bound can never be
                // judged or displayed. They must not hold the outro gate shut.
                outroBeyondSong++;
            else
            {
                outroPlayableComponents++;
                // A sustain that reaches the song-end bound leaves no blank
                // outro to skip; keep the gate closed until normal song end.
                lastPlayableTailEnd = Math.Max(lastPlayableTailEnd,
                    Math.Min(noteTime + sustainLength, game.songLength));
            }
            outroComponents++;
            outroCursor++;
        }
        return outroTimingValid && outroPlayableComponents > 0 && outroCursor == notes.Count
            && game.songTime >= lastPlayableTailEnd + .25;
    }

    public LiveStats Read(Engine engine, GameManager game)
    {
        // Read only. Inspect the same flattened chart used by the results snapshot.
        // Do not cache native note pointers across frames or scene changes.
        var notes = engine.prop_List_1_ObjectPublicObInObDoSiDoUIInBoInUnique_0;
        int count = notes.Count;
        bool practice = game.globalVariables.isPracticeEnabled;
        long start = practice ? game.practiceDisplayStartTick : long.MinValue;
        long end = practice ? game.practiceDisplayEndTick : long.MaxValue;
        int hits = engine.field_Public_Int32_6;
        if (engine.Pointer != enginePointer || notes.Pointer != notesPointer || cursor > count
            || game.songTime < lastSongTime - .05 || hits < lastHits || start != rangeStart || end != rangeEnd)
        {
            // Ordered chart ticks let practice jump directly to its first head
            // instead of marshalling thousands of previous notes after a seek.
            cursor = practice ? OrderedNoteSearch.LowerBound(count, start,
                i => notes[i]?.field_Public_Int64_0 ?? long.MinValue) : 0;
            resolvedHeads = 0;
            practiceMissedHeads = 0;
            recentPracticeMisses.Clear();
            enginePointer = engine.Pointer;
            notesPointer = notes.Pointer;
            rangeStart = start;
            rangeEnd = end;
        }
        lastSongTime = game.songTime;
        lastHits = hits;
        if (practice)
        {
            // The native hit count can include heads outside this section.
            // Revisit recently missed heads because a late accepted hit can
            // correct the game's earlier missed state.
            for (int i = recentPracticeMisses.Count - 1; i >= 0; i--)
            {
                var note = notes[recentPracticeMisses[i]];
                if (note == null) { recentPracticeMisses.RemoveAt(i); continue; }
                ushort state = (ushort)note.field_Public_EnumNPublicSealedvaNoHiMiDyIsWa8vWaUnique_0;
                if (ChartNote.IsHit(state))
                {
                    practiceMissedHeads--;
                    recentPracticeMisses.RemoveAt(i);
                }
                else if (note.field_Public_Double_0 < game.songTime - 2)
                    recentPracticeMisses.RemoveAt(i);
            }
        }
        // Chart is in time order. Process each resolved head once; unresolved
        // future notes wait for the next refresh. Bound catch-up after a seek.
        for (int budget = 4096; cursor < count && budget > 0; budget--)
        {
            var note = notes[cursor];
            if (note == null) { cursor++; continue; }
            long tick = note.field_Public_Int64_0;
            if (tick < start) { cursor++; continue; }
            if (tick > end) break;
            uint flags = (uint)note.field_Public_EnumNPublicSealedvaNoChDiExChHoStTaSoUnique_0;
            ushort state = (ushort)note.field_Public_EnumNPublicSealedvaNoHiMiDyIsWa8vWaUnique_0;
            // Disjoint child heads are omitted by the game's results denominator.
            if (!ChartNote.CountsTowardAccuracy(flags)) { cursor++; continue; }
            if (!ChartNote.IsResolved(state)) break;
            if (practice && ChartNote.IsMissedWithoutHit(state))
            {
                practiceMissedHeads++;
                recentPracticeMisses.Add(cursor);
            }
            // Count each judged head once. A note can first be marked missed
            // and then accepted as a late hit; the native hit counter reflects
            // that correction, while a cached miss decision would not.
            resolvedHeads++;
            cursor++;
        }
        // The game's results screen computes misses as eligible heads minus
        // native hits. Use judged heads here so future notes stay excluded.
        // The ghost counter is the same native field PracticeUI reads.
        long sectionHits = resolvedHeads - practiceMissedHeads;
        return LiveStats.FromResolvedHeads(practice ? sectionHits : hits,
            resolvedHeads, engine.field_Public_Int32_17);
    }
}
