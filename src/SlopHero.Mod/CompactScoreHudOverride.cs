using Il2Cpp;
using MelonLoader;
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
            // This field occupies the old SpriteFont.lastAmount slot, directly
            // after maxValue in the current generated class. Negative means
            // the font has not emitted its first value yet.
            score = scoreFont.field_Private_Int32_0;
            combo = comboFont.field_Private_Int32_0;
            return score >= 0 && combo >= 0 && score <= scoreFont.maxValue && combo <= comboFont.maxValue;
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

    private bool Capture(GameManager game)
    {
        var manager = game.scoreManager;
        var score = manager?.scoreFont?.transform;
        var combo = manager?.comboFont?.transform;
        if (!TryRead(game, out int scoreValue, out int comboValue)
            || !MatchesLastDigit(manager?.scoreFont, scoreValue)
            || !MatchesLastDigit(manager?.comboFont, comboValue)) return false;
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

    private static bool MatchesLastDigit(SpriteFont? font, int amount)
    {
        // Guard the older-source lastAmount mapping against a changed current
        // build. The least-significant glyph must agree with the cached int
        // before any native score renderer is hidden.
        var digits = font?.Sprites;
        var glyphs = font?.fontSprites;
        if (digits == null || glyphs == null || digits.Length == 0 || glyphs.Length < 10) return false;
        var first = digits[0];
        var expected = glyphs[amount % 10];
        return first != null && expected != null && first.sprite != null
            && first.sprite.Pointer == expected.Pointer;
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
