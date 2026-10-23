using Il2Cpp;
using UnityEngine;

namespace SlopHero.Mod;

// The current 1.1 build has no exposed GlobalVariables.isAprilFools field.
// Its shared BaseMenu.HAVEFUN state and PongersGame component are the current
// generated-assembly hooks for the disruptive joke content.
internal sealed class AprilFoolsOverrides
{
    private readonly Dictionary<IntPtr, (PongersGame Game, bool WasActive)> hiddenPongGames = new();
    private readonly Dictionary<IntPtr, (GlobalVariables State, bool WasEnabled)> noteBomberStates = new();
    private double nextPongScan;
    private bool savedHaveFun;
    private bool applied;

    public void Apply(bool disable, double realtime, bool scanPong)
    {
        if (!disable)
        {
            Restore();
            return;
        }

        if (!applied)
        {
            savedHaveFun = BaseMenu.HAVEFUN;
            applied = true;
            nextPongScan = 0;
        }

        // Preserve a true value that the game sets after mod initialization,
        // so turning this option back off restores the game's current state.
        if (BaseMenu.HAVEFUN) savedHaveFun = true;
        BaseMenu.HAVEFUN = false;

        var globals = GlobalVariables.instance;
        if (globals != null)
        {
            if (!noteBomberStates.ContainsKey(globals.Pointer))
                noteBomberStates.Add(globals.Pointer, (globals, globals.isNoteBomber));
            var state = noteBomberStates[globals.Pointer];
            if (globals.isNoteBomber && !state.WasEnabled)
                noteBomberStates[globals.Pointer] = (globals, true);
            globals.isNoteBomber = false;
        }

        // The Pong object is a joke minigame, not a gameplay service. Search
        // infrequently and only while suppression is enabled; disabling its
        // GameObject stops its own updates and rendering without destroying it.
        // Pong is menu content. The Gameplay scene contains no Pong object,
        // so avoid a scene-wide FindObjectsOfType scan during a chart. When
        // returning to a menu, the scan runs immediately if it is overdue.
        if (!scanPong || realtime < nextPongScan) return;
        nextPongScan = realtime + 2.0;
        foreach (var game in UnityEngine.Object.FindObjectsOfType<PongersGame>())
        {
            if (game == null || game.gameObject == null) continue;
            var gameObject = game.gameObject;
            if (!gameObject.activeSelf) continue;
            if (!hiddenPongGames.ContainsKey(game.Pointer))
                hiddenPongGames.Add(game.Pointer, (game, gameObject.activeSelf));
            gameObject.SetActive(false);
        }
    }

    public void Restore()
    {
        foreach (var state in noteBomberStates.Values)
        {
            try { if (state.State != null) state.State.isNoteBomber = state.WasEnabled; }
            catch { }
        }
        noteBomberStates.Clear();
        foreach (var state in hiddenPongGames.Values)
        {
            try
            {
                if (state.Game != null && state.Game.gameObject != null)
                    state.Game.gameObject.SetActive(state.WasActive);
            }
            catch { }
        }
        hiddenPongGames.Clear();
        try { if (applied) BaseMenu.HAVEFUN = savedHaveFun; }
        catch { }
        applied = false;
        nextPongScan = 0;
    }
}
