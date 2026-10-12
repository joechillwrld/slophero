using Il2Cpp;
using SlopHero.Core;
using Engine = Il2Cpp.ObjectPublicAbstractDoBoDoInBoObDoInSiBoUnique;

namespace SlopHero.Mod;

internal sealed class ChartStats
{
    private IntPtr enginePointer, notesPointer;
    private int cursor;
    private long misses, rangeStart, rangeEnd;
    private double lastSongTime = double.NegativeInfinity;
    private int lastHits;

    public LiveStats Read(Engine engine, GameManager game)
    {
        // Read only. Inspect the same flattened chart used by the results snapshot.
        // Do not cache native note pointers across frames or scene changes.
        var notes = engine.prop_List_1_ObjectPublicObInObDoSiDoUIInBoInUnique_0;
        bool practice = game.globalVariables.isPracticeEnabled;
        long start = practice ? game.practiceDisplayStartTick : long.MinValue;
        long end = practice ? game.practiceDisplayEndTick : long.MaxValue;
        int hits = engine.field_Public_Int32_6;
        if (engine.Pointer != enginePointer || notes.Pointer != notesPointer || cursor > notes.Count
            || game.songTime < lastSongTime - .05 || hits < lastHits || start != rangeStart || end != rangeEnd)
        {
            cursor = 0;
            misses = 0;
            enginePointer = engine.Pointer;
            notesPointer = notes.Pointer;
            rangeStart = start;
            rangeEnd = end;
        }
        lastSongTime = game.songTime;
        lastHits = hits;
        // Chart is in time order. Process each resolved head once; unresolved
        // future notes wait for the next refresh. Bound catch-up after a seek.
        for (int budget = 4096; cursor < notes.Count && budget > 0; budget--)
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
            if (ChartNote.IsMissed(state)) misses++;
            cursor++;
        }
        // Native engine offsets 0x9C and 0xD4 respectively, accessed through
        // generated interop properties rather than hard-coded pointer reads.
        return new LiveStats(hits, misses, engine.field_Public_Int32_17);
    }
}
