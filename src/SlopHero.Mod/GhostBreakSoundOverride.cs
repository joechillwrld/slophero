using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using SlopHero.Core;

namespace SlopHero.Mod;

/// <summary>Silence LoseMultiplier only when a local ghost input broke the combo.</summary>
internal sealed class GhostBreakSoundOverride
{
    private static GhostBreakSoundOverride? active;
    private readonly List<MethodInfo> targets = new();
    private readonly ChartStats chartStats = new();
    private GameManager? game;
    private IntPtr enginePointer;
    private long previousGhosts, previousMisses;
    private bool baselineValid, installed, failed, eligible;
    private int eligibleMultiplierSounds, suppressedGhostSounds;

    internal void Update(HarmonyLib.Harmony harmony, bool requested, bool soloEligible, GameManager? currentGame)
    {
        if (!requested || !soloEligible || currentGame?.playerObjects == null)
        {
            eligible = false;
            baselineValid = false;
            game = null;
            if (!requested && installed) Uninstall(harmony);
            return;
        }
        if (failed) return;
        if (!installed) Install(harmony);
        if (!installed) return;
        var player = currentGame.playerObjects.FirstOrDefault(p => p != null && p.enabled
            && p.TryCast<GuitarPlayer>() != null && p.engine != null
            && p.player != null && p.player.field_Public_Player_0 != null);
        if (player?.engine == null) { eligible = false; baselineValid = false; return; }
        var engine = player.engine;
        if (engine.Pointer != enginePointer || game?.Pointer != currentGame.Pointer)
        {
            enginePointer = engine.Pointer;
            baselineValid = false;
        }
        game = currentGame;
        eligible = true;
        LiveStats stats = chartStats.Read(engine, currentGame);
        previousGhosts = stats.GhostInputs;
        previousMisses = stats.Misses;
        baselineValid = true;
    }

    private void Install(HarmonyLib.Harmony harmony)
    {
        const string stem = "Method_Public_Void_EnumPublicSealedvaNoBeStOvStGhOvLoStUnique_";
        try
        {
            active = this;
            var prefix = new HarmonyMethod(typeof(GhostBreakSoundOverride).GetMethod(
                nameof(BeforeSound), BindingFlags.NonPublic | BindingFlags.Static)!);
            foreach (string suffix in new[] { "Single_PDM_0", "Single_0", "Int16_Single_0" })
            {
                string name = stem + suffix;
                var target = typeof(BassAudioManager).GetMethod(name,
                    BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new MissingMethodException(typeof(BassAudioManager).FullName, name);
                harmony.Patch(target, prefix: prefix);
                targets.Add(target);
            }
            installed = true;
            MelonLogger.Msg("Ghost-only combo-break sound filter installed.");
        }
        catch (Exception ex)
        {
            Uninstall(harmony);
            failed = true;
            MelonLogger.Warning($"Ghost combo-break sound filter unavailable; all game sounds retained: {ex.Message}");
        }
    }

    private static bool BeforeSound(EnumPublicSealedvaNoBeStOvStGhOvLoStUnique __0)
    {
        var filter = active;
        if (filter == null || !filter.eligible || !filter.baselineValid
            || __0 != EnumPublicSealedvaNoBeStOvStGhOvLoStUnique.LoseMultiplier)
            return true;
        try
        {
            filter.eligibleMultiplierSounds++;
            bool suppress = filter.IsGhostOnlyBreak();
            if (suppress) filter.suppressedGhostSounds++;
            return !suppress;
        }
        catch (Exception ex)
        {
            filter.eligible = false;
            MelonLogger.Warning($"Ghost sound filter stopped after a game binding error; normal sound retained: {ex.Message}");
            return true;
        }
    }

    private bool IsGhostOnlyBreak()
    {
        var currentGame = game;
        if (currentGame?.playerObjects == null) return false;
        var player = currentGame.playerObjects.FirstOrDefault(p => p != null && p.enabled
            && p.engine?.Pointer == enginePointer && p.player?.field_Public_Player_0 != null);
        if (player?.engine == null) return false;
        LiveStats stats = chartStats.Read(player.engine, currentGame);
        bool ghostOnly = GhostSoundDecision.SuppressLoseMultiplier(previousGhosts,
            stats.GhostInputs, previousMisses, stats.Misses);
        previousGhosts = stats.GhostInputs;
        previousMisses = stats.Misses;
        return ghostOnly;
    }

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        Uninstall(harmony);
        failed = false;
        enginePointer = IntPtr.Zero;
        game = null;
        baselineValid = eligible = false;
    }

    private void Uninstall(HarmonyLib.Harmony harmony)
    {
        eligible = false;
        if (eligibleMultiplierSounds != 0)
            MelonLogger.Msg($"Ghost sound filter: {suppressedGhostSounds} of {eligibleMultiplierSounds} multiplier-loss sound calls suppressed.");
        eligibleMultiplierSounds = suppressedGhostSounds = 0;
        if (ReferenceEquals(active, this)) active = null;
        foreach (var target in targets)
            try { harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"Ghost sound hook cleanup failed: {ex.Message}"); }
        targets.Clear();
        installed = false;
    }
}
