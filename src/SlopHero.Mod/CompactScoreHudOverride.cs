using Il2Cpp;
using MelonLoader;
using SlopHero.Core;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Optional solo HUD experiment. SpriteFont keeps producing the native score
/// and combo digits; only their 23 renderers are hidden after the replacement
/// Canvas is confirmed visible. The game's score and combo logic is untouched.
/// </summary>
internal sealed class CompactScoreHudOverride
{
    private readonly record struct SpriteState(SpriteRenderer Renderer, bool Enabled);
    private readonly List<SpriteState> sprites = new(23);
    private bool failed;

    internal bool IsActive => sprites.Count != 0;

    // SpriteFont.SetSprites may re-enable a digit after our 20 Hz layout pass.
    // Reassert immediately before each rendered frame while the Canvas is up.
    internal void Reassert()
    {
        try
        {
            foreach (var state in sprites)
                if (state.Renderer != null && state.Renderer.enabled)
                    state.Renderer.enabled = false;
        }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            MelonLogger.Warning($"Compact score HUD stopped; native score graphics restored: {ex.Message}");
        }
    }

    internal static bool TryRead(GameManager? game, out int score, out int combo)
    {
        score = combo = 0;
        try
        {
            var manager = game?.scoreManager;
            var scoreFont = manager?.scoreFont;
            var comboFont = manager?.comboFont;
            if (scoreFont == null || comboFont == null || !scoreFont.isActiveAndEnabled
                || !comboFont.isActiveAndEnabled) return false;
            // Read the digits the game actually drew. The private lastAmount
            // field was identified from an older build and can move in IL2CPP;
            // using it kept the replacement inactive while native art showed.
            return TryReadDisplayed(scoreFont, out score) && TryReadDisplayed(comboFont, out combo);
        }
        catch { return false; }
    }

    internal void Update(bool requested, bool eligible, bool canvasVisible, GameManager? game)
    {
        if (!requested || !eligible || !canvasVisible || game == null)
        {
            Restore();
            return;
        }
        if (failed) return;
        try
        {
            if (sprites.Count == 0 && !Capture(game)) return;
            Reassert();
        }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            MelonLogger.Warning($"Compact score HUD stopped; native score graphics restored: {ex.Message}");
        }
    }

    private bool Capture(GameManager game)
    {
        var manager = game.scoreManager;
        var score = manager?.scoreFont?.transform;
        var combo = manager?.comboFont?.transform;
        if (!TryRead(game, out _, out _)) return false;
        if (score == null || combo == null || score.name != "Score" || combo.name != "Combo"
            || score.parent == null || combo.parent == null
            || score.parent.Pointer != combo.parent.Pointer || score.parent.name != "HUD"
            || score.parent.parent == null || score.parent.parent.name != "HUDCam") return false;
        var scoreSprites = score.GetComponentsInChildren<SpriteRenderer>(true);
        var comboSprites = combo.GetComponentsInChildren<SpriteRenderer>(true);
        // Exact current Gameplay scene: 15 score and 8 combo sprites. If a
        // skin/mod changes the structure, keep the native display intact.
        if (scoreSprites.Length != 15 || comboSprites.Length != 8) return false;
        foreach (var renderer in scoreSprites)
            if (renderer != null) sprites.Add(new SpriteState(renderer, renderer.enabled));
        foreach (var renderer in comboSprites)
            if (renderer != null) sprites.Add(new SpriteState(renderer, renderer.enabled));
        if (sprites.Count != 23)
        {
            sprites.Clear();
            return false;
        }
        MelonLogger.Msg("Compact score HUD replaced 23 native score/combo renderers with Canvas text for this solo song.");
        return true;
    }

    private static bool TryReadDisplayed(SpriteFont font, out int amount)
    {
        amount = 0;
        var digits = font.Sprites;
        var glyphs = font.fontSprites;
        if (digits == null || glyphs == null || digits.Length == 0 || glyphs.Length < 10) return false;
        if (digits.Length > 32) return false;
        Span<int> slots = stackalloc int[digits.Length];
        // SpriteFont stores the least significant digit first. Comma slots
        // consume an array entry but no decimal place.
        for (int slot = 0; slot < digits.Length; slot++)
        {
            var renderer = digits[slot];
            if (renderer == null) return false;
            var sprite = renderer.sprite;
            if (sprite == null) { slots[slot] = -1; continue; }
            int digit = -1;
            for (int i = 0; i < 10; i++)
                if (glyphs[i] != null && glyphs[i].Pointer == sprite.Pointer) { digit = i; break; }
            if (digit < 0)
            {
                if (font.drawCommas && glyphs.Length > 10 && glyphs[10] != null
                    && glyphs[10].Pointer == sprite.Pointer) { slots[slot] = 10; continue; }
                return false;
            }
            slots[slot] = digit;
        }
        return DisplayedSpriteNumber.TryRead(slots, out amount);
    }

    internal void Restore()
    {
        foreach (var state in sprites)
            try { if (state.Renderer != null) state.Renderer.enabled = state.Enabled; }
            catch { }
        sprites.Clear();
    }

    internal void Clear()
    {
        Restore();
        failed = false;
    }
}
