using Il2CppTMPro;
using SlopHero.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SlopHero.Mod;

/// <summary>
/// Optional retained counter HUD. The existing IMGUI renderer remains the
/// fallback if this game's stripped Unity UI wrappers reject construction.
/// </summary>
internal sealed class CounterCanvasHud
{
    private GameObject? root;
    private RectTransform? panelRect;
    private Image? background;
    private TextMeshProUGUI? label;
    private string? shownText;
    private Color shownTextColor, shownBackgroundColor;
    private int shownFont = -1, shownX = -1, shownY = -1, shownWidth = -1, shownHeight = -1;
    private bool textColorConfigured, backgroundConfigured;
    private bool failed;

    public bool Visible { get; private set; }
    public bool HasObjects => root != null;

    public string? Refresh(bool enabled, bool shouldShow, string text, int x, int y,
        int fontSize, Color textColor, float opacity)
    {
        if (!enabled)
        {
            Clear();
            return null;
        }
        if (!shouldShow || string.IsNullOrEmpty(text))
        {
            Hide();
            return null;
        }
        if (failed) return null;

        try
        {
            if (root == null) Create();
            if (root == null || panelRect == null || background == null || label == null)
                throw new InvalidOperationException("Canvas HUD components were not created.");

            int size = Math.Clamp(fontSize, 14, 48);
            int width = Math.Max(240, size * 12 + 24);
            int lines = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lines++;
            int height = (int)(size * 1.25f) * lines + 20;
            var position = HudLayout.ClampPosition(x, y, width, height, Screen.width, Screen.height);
            if (shownX != position.X || shownY != position.Y || shownWidth != width || shownHeight != height)
            {
                panelRect.anchoredPosition = new Vector2(position.X, -position.Y);
                panelRect.sizeDelta = new Vector2(width, height);
                shownX = position.X; shownY = position.Y; shownWidth = width; shownHeight = height;
            }
            if (!string.Equals(shownText, text, StringComparison.Ordinal))
            {
                label.text = text;
                shownText = text;
            }
            if (shownFont != size) { label.fontSize = size; shownFont = size; }
            if (!textColorConfigured || shownTextColor != textColor)
            { label.color = textColor; shownTextColor = textColor; textColorConfigured = true; }
            var panelColor = new Color(0, 0, 0, HudLayout.Unit(opacity, .65f));
            if (!backgroundConfigured || shownBackgroundColor != panelColor)
            {
                background.color = panelColor;
                background.enabled = panelColor.a > .001f;
                shownBackgroundColor = panelColor;
                backgroundConfigured = true;
            }
            if (!root.activeSelf) root.SetActive(true);
            if (label.font == null)
                throw new InvalidOperationException("Clone Hero did not provide a default TextMeshPro font.");
            Visible = true;
            return null;
        }
        catch (Exception ex)
        {
            failed = true;
            Clear();
            return ex.ToString();
        }
    }

    public void Hide()
    {
        if (!Visible) return;
        Visible = false;
        try { if (root != null) root.SetActive(false); } catch { }
    }

    public void Clear()
    {
        Visible = false;
        try
        {
            if (root != null)
            {
                try { root.SetActive(false); } catch { }
                UnityEngine.Object.Destroy(root);
            }
        }
        catch { }
        root = null;
        panelRect = null;
        background = null;
        label = null;
        shownText = null;
        shownFont = shownX = shownY = shownWidth = shownHeight = -1;
        textColorConfigured = backgroundConfigured = false;
    }

    private void Create()
    {
        root = new GameObject("SlopHero Counter Canvas");
        root.SetActive(false);
        root.AddComponent<RectTransform>();
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;

        var panel = new GameObject("Counter background");
        panel.SetActive(false);
        try { panel.transform.SetParent(root.transform, false); }
        catch { UnityEngine.Object.Destroy(panel); throw; }
        panel.AddComponent<RectTransform>();
        panelRect = panel.GetComponent<RectTransform>();
        if (panelRect == null) throw new InvalidOperationException("Counter panel has no RectTransform.");
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0, 1);
        background = panel.AddComponent<Image>();
        background.raycastTarget = false;

        var textObject = new GameObject("Counter text");
        try { textObject.transform.SetParent(panel.transform, false); }
        catch { UnityEngine.Object.Destroy(textObject); throw; }
        textObject.AddComponent<RectTransform>();
        var textRect = textObject.GetComponent<RectTransform>();
        if (textRect == null) throw new InvalidOperationException("Counter text has no RectTransform.");
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12, 8);
        textRect.offsetMax = new Vector2(-12, -8);
        label = textObject.AddComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.enableWordWrapping = false;
        panel.SetActive(true);
    }
}
