using Il2Cpp;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Optional skip for the six leaderboard motion checks while their parent
/// score list is fully invisible. Entry data, network requests, and rendering
/// are left alone. Components are restored on the next 60 Hz visibility check.
/// </summary>
internal sealed class HiddenLeaderboardOverride
{
    private readonly record struct EntryState(InGameLeaderboardEntry Entry, bool Enabled);

    private EntryState[] entries = Array.Empty<EntryState>();
    private GameObject? scoreList;
    private CanvasGroup? group;
    private double nextDiscovery;

    public bool NeedsRestore => entries.Length != 0;

    public void Apply(bool requested, double realtime)
    {
        if (!requested)
        {
            Restore();
            nextDiscovery = 0;
            return;
        }

        if (scoreList == null || group == null)
        {
            if (entries.Length != 0)
            {
                Restore();
                nextDiscovery = 0;
            }
            if (realtime < nextDiscovery) return;
            Discover(realtime);
        }
        if (scoreList == null || group == null || entries.Length == 0) return;

        // Keep online score displays fully under the game's control. In solo
        // play, skip motion checks only after the entire group has faded out.
        var network = CHNetManager.instance;
        bool online = network != null && (int)network.field_Private_ConnectionState_0 != 0;
        bool hidden = scoreList.activeInHierarchy && group.alpha <= .001f;
        bool suspend = !online && hidden;
        foreach (var state in entries)
        {
            var entry = state.Entry;
            if (entry == null) continue;
            bool desired = state.Enabled && !suspend;
            if (entry.enabled != desired) entry.enabled = desired;
        }
    }

    public void Clear()
    {
        Restore();
        nextDiscovery = 0;
    }

    private void Discover(double realtime)
    {
        nextDiscovery = realtime + 1;
        try
        {
            var root = GameObject.Find("HUDCam");
            var list = root == null ? null : root.transform.Find("Leaderboard/Canvas/ScoreList");
            if (list == null) return;
            var canvasGroup = list.GetComponent<CanvasGroup>();
            var found = list.GetComponentsInChildren<InGameLeaderboardEntry>(true);
            if (canvasGroup == null || found.Length == 0) return;

            scoreList = list.gameObject;
            group = canvasGroup;
            entries = found.Select(entry => new EntryState(entry, entry != null && entry.enabled)).ToArray();
            nextDiscovery = double.MaxValue;
        }
        catch
        {
            // Retry on a later 20 Hz pass. This is an optional visual-only
            // optimization, so a missing wrapper must never affect the song.
            scoreList = null;
            group = null;
            entries = Array.Empty<EntryState>();
        }
    }

    private void Restore()
    {
        foreach (var state in entries)
        {
            try
            {
                if (state.Entry != null && state.Entry.enabled != state.Enabled)
                    state.Entry.enabled = state.Enabled;
            }
            catch { }
        }
        entries = Array.Empty<EntryState>();
        scoreList = null;
        group = null;
    }
}
