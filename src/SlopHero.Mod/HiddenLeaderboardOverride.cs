using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlopHero.Mod;

/// <summary>
/// Optional pause for hidden leaderboard entries, layout, and TMP labels.
/// Entry data and network requests stay under the game's control. Components
/// are restored within 50 ms of the list appearing.
/// </summary>
internal sealed class HiddenLeaderboardOverride
{
    private readonly record struct EntryState(InGameLeaderboardEntry Entry, bool Enabled);
    private readonly record struct FitterState(AspectRatioFitter Fitter, bool Enabled);
    private readonly record struct TextState(TextMeshProUGUI Text, bool Enabled);

    private EntryState[] entries = Array.Empty<EntryState>();
    private FitterState[] fitters = Array.Empty<FitterState>();
    private TextState[] texts = Array.Empty<TextState>();
    private SpectatorCount? spectator;
    private bool spectatorEnabled;
    private GameObject? scoreList;
    private CanvasGroup? group;
    private double nextDiscovery;
    private double nextReconcile;
    private bool? lastHidden;

    public bool NeedsRestore => entries.Length != 0 || fitters.Length != 0
        || texts.Length != 0 || spectator != null;

    public void Apply(bool requested, double realtime, bool online)
    {
        if (!requested || online)
        {
            Restore();
            nextDiscovery = online ? realtime + 1 : 0;
            return;
        }

        if (scoreList == null || group == null)
        {
            if (NeedsRestore)
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
        bool hidden = scoreList.activeInHierarchy && group.alpha <= .001f;
        if (lastHidden == hidden && realtime < nextReconcile) return;
        lastHidden = hidden;
        nextReconcile = realtime + 1;
        bool suspend = hidden;
        foreach (var state in entries)
        {
            var entry = state.Entry;
            if (entry == null) continue;
            bool desired = state.Enabled && !suspend;
            if (entry.enabled != desired) entry.enabled = desired;
        }
        foreach (var state in fitters)
        {
            var fitter = state.Fitter;
            if (fitter == null) continue;
            bool desired = state.Enabled && !suspend;
            if (fitter.enabled != desired) fitter.enabled = desired;
        }
        // The score list keeps its TMP labels enabled under an alpha-zero
        // CanvasGroup. Stop their rebuild/scale callbacks while hidden, then
        // restore their original enabled states with the entries and fitters.
        foreach (var state in texts)
        {
            var label = state.Text;
            if (label == null) continue;
            bool desired = state.Enabled && !suspend;
            if (label.enabled != desired) label.enabled = desired;
        }
        // SpectatorCount.FixedUpdate polls the online count even in solo play.
        // It is independent of the score-list fade, but needed immediately
        // again if the session becomes online.
        if (spectator != null && spectator.enabled) spectator.enabled = false;
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
            if (root == null) return;
            var list = root.transform.Find("Leaderboard/Canvas/ScoreList");
            if (list == null) return;
            var canvasGroup = list.GetComponent<CanvasGroup>();
            var found = list.GetComponentsInChildren<InGameLeaderboardEntry>(true);
            if (canvasGroup == null || found.Length == 0) return;
            var foundFitters = list.GetComponentsInChildren<AspectRatioFitter>(true);
            var foundTexts = list.GetComponentsInChildren<TextMeshProUGUI>(true);

            scoreList = list.gameObject;
            group = canvasGroup;
            entries = found.Select(entry => new EntryState(entry, entry != null && entry.enabled)).ToArray();
            fitters = foundFitters.Select(fitter => new FitterState(fitter, fitter != null && fitter.enabled)).ToArray();
            texts = foundTexts.Select(text => new TextState(text, text != null && text.enabled)).ToArray();
            spectator = root.transform.Find("Menu Canvas/Spectator Count")?.GetComponent<SpectatorCount>();
            spectatorEnabled = spectator != null && spectator.enabled;
            lastHidden = null;
            nextDiscovery = double.MaxValue;
        }
        catch
        {
            // Retry on a later 20 Hz pass. This is an optional visual-only
            // optimization, so a missing wrapper must never affect the song.
            scoreList = null;
            group = null;
            entries = Array.Empty<EntryState>();
            fitters = Array.Empty<FitterState>();
            texts = Array.Empty<TextState>();
            spectator = null;
            spectatorEnabled = false;
            lastHidden = null;
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
        foreach (var state in fitters)
        {
            try
            {
                if (state.Fitter != null && state.Fitter.enabled != state.Enabled)
                    state.Fitter.enabled = state.Enabled;
            }
            catch { }
        }
        foreach (var state in texts)
        {
            try
            {
                if (state.Text != null && state.Text.enabled != state.Enabled)
                    state.Text.enabled = state.Enabled;
            }
            catch { }
        }
        try
        {
            if (spectator != null && spectator.enabled != spectatorEnabled)
                spectator.enabled = spectatorEnabled;
        }
        catch { }
        entries = Array.Empty<EntryState>();
        fitters = Array.Empty<FitterState>();
        texts = Array.Empty<TextState>();
        spectator = null;
        spectatorEnabled = false;
        scoreList = null;
        group = null;
        nextReconcile = 0;
        lastHidden = null;
    }
}
